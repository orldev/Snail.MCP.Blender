"""TCP listener: newline-delimited JSON in, one JSON line per request out, on the loopback unless a container names another address.

Blender's Python API is not thread-safe, so the socket threads never touch bpy. They parse
requests and queue them; the main thread drains the queue, through a bpy.app.timers callback with the interface open or the
loop of a headless Blender without one, and writes the replies. Only commands registered as immediate are answered from the
socket thread. A request whose client has gone by the time its turn comes is skipped: the client cancelled or timed out, so
nobody waits for it. So is one whose own deadline passed while it waited: a server says with each request how long its caller
will wait, and a command run for nobody still changes the scene the next caller reads.

Several clients may be connected at once. The server holds three: the control link, a monitor link that asks for progress
while a render blocks the main thread, and a data link for files and the data directory. Which of them carries a command is
written in commands.json, and the commands it names there are exactly the ones registered as immediate here.
The token must ride on every request, because anything on this machine, or on a container's network, can open the socket; a
request with the wrong one is refused and its connection closed, so each further guess costs a new connection.

Each connection is two threads and a queue: one reads its lines, one writes its replies. Nothing else ever waits on its socket, so
a client that stops reading holds up neither the main thread nor another client, and when its unsent replies pass the outbox limits
the connection is dropped rather than grown. Replies are encoded so that no string can fail to encode: one that did used to raise
out of the loop and end a Blender without an interface.
"""

import contextlib
import functools
import hmac
import json
import queue
import socket
import threading
import time
import traceback

import bpy

MAX_LINE_BYTES = 16 * 1024 * 1024
MAX_QUEUED_REQUESTS = 64
MOST_CLIENTS = 16
SILENT_CLIENT_SECONDS = 30
MAX_REPLY_BYTES = 4 * 1024 * 1024
PUMP_INTERVAL_SECONDS = 0.05
SEND_TIMEOUT_SECONDS = 60
MOST_QUEUED_REPLIES = 64
END = object()
MOST_QUEUED_BYTES = 16 * 1024 * 1024
TRACEBACK_LINES = 12


def _expires_at(request):
    """When this request stops being worth running, from the seconds its caller said it would wait.

    The clocks of the two machines need not agree: the number is a duration, counted from the moment the request arrived here. A
    request without one never expires, which is what an older server and a client of one's own send.
    """
    deadline = request.get("deadline_s")
    if not isinstance(deadline, (int, float)) or isinstance(deadline, bool) or deadline <= 0:
        return None
    return time.monotonic() + float(deadline)


class CommandError(Exception):
    """A failure the caller can act on: carries a short type and a message."""

    def __init__(self, kind, message, details=None):
        super().__init__(message)
        self.kind = kind
        self.details = details


class Connection:
    """One client: a thread that reads its lines and a thread that writes its replies.

    Writing happens here and nowhere else. The main thread used to write replies itself, so a client that stopped reading a large one
    held up Blender until the send timed out; now it only leaves the reply in this outbox. A client whose outbox passes its limits is
    dropped, because the alternative is to grow it inside Blender for a client that may never read again.
    """

    def __init__(self, socket, serve, gone):
        self._socket = socket
        self._serve = serve
        self._gone = gone
        self._outbox = queue.Queue(maxsize=MOST_QUEUED_REPLIES)
        self._arrived = time.monotonic()
        self._asked = False
        self._waiting = 0
        self._gate = threading.Lock()
        self._running = True

    def start(self):
        self._socket.settimeout(SEND_TIMEOUT_SECONDS)
        threading.Thread(target=self._write, name="snail-bridge-write", daemon=True).start()
        threading.Thread(target=self._read, name="snail-bridge-read", daemon=True).start()

    @property
    def is_open(self):
        return self._running

    def is_silent(self, seconds):
        """Has asked nothing since it arrived, for this long: a peer holding threads and a socket for no work."""
        return not self._asked and time.monotonic() - self._arrived > seconds

    def send(self, data):
        with self._gate:
            if not self._running or self._waiting + len(data) > MOST_QUEUED_BYTES:
                self.close()
                return
            self._waiting += len(data)
        try:
            self._outbox.put_nowait(data)
        except queue.Full:
            self.close()

    def close(self):
        """Ends the link now: what is still in the outbox is lost, which is the point when the client is the reason it piled up."""
        if not self._running:
            return
        self._running = False
        with contextlib.suppress(queue.Full):
            self._outbox.put_nowait(None)
        self._shutdown()

    def finish(self):
        """Ends the link after the replies already queued have left, so a refusal reaches the client that earned it."""
        if not self._running:
            return
        self._running = False
        try:
            self._outbox.put_nowait(END)
        except queue.Full:
            self._shutdown()

    def _shutdown(self):
        with contextlib.suppress(OSError):
            self._socket.shutdown(socket.SHUT_RDWR)

    def _write(self):
        """Owns the socket to the end: the reader may stop at any moment, and closing under a reply still going out would cut it in half."""
        while True:
            data = self._outbox.get()
            if data is None or data is END:
                self._shutdown()
                self._socket.close()
                return
            with self._gate:
                self._waiting -= len(data)
            try:
                self._socket.sendall(data)
            except OSError:
                self.close()
                return

    def _read(self):
        buffer = bytearray()
        try:
            while self._running:
                try:
                    chunk = self._socket.recv(65536)
                except TimeoutError:
                    continue
                except OSError:
                    return
                if not chunk:
                    return
                buffer += chunk
                if len(buffer) > MAX_LINE_BYTES:
                    self._serve(self, None)
                    buffer.clear()
                    continue
                while (cut := buffer.find(b"\n")) >= 0:
                    line = bytes(buffer[:cut])
                    del buffer[:cut + 1]
                    self._asked = True
                    try:
                        self._serve(self, line)
                    except Exception:
                        traceback.print_exc()
        finally:
            self.close()
            self._gone(self)


class BridgeServer:
    def __init__(self, host, port, dispatch, immediate, token=""):
        self.host = host
        self.port = port
        self._token = token or ""
        self._dispatch = dispatch
        self._immediate = immediate
        self._listener = None
        self._clients = set()
        self._client_lock = threading.Lock()
        self._pending = queue.Queue(maxsize=MAX_QUEUED_REQUESTS)
        self._running = False
        self._executing = None
        self._pumped_at = None
        self._thread = None
        self._timer = self.pump

    @property
    def is_running(self):
        return self._running

    @property
    def has_client(self):
        with self._client_lock:
            return len(self._clients) > 0

    @property
    def executing(self):
        return self._executing

    @property
    def queued(self):
        return self._pending.qsize()

    @property
    def since_pumped(self):
        """Seconds since the main thread last drained the queue, or None before the first tick.

        This is the pulse of the dispatcher, and the one thing a client outside Blender cannot see for itself: a Blender whose timer died
        answers ping from the socket thread exactly like a healthy one, and looks merely quiet. Read while a command is running it only
        says how long that command has been going, which is why what reads it also asks what is executing.
        """
        return None if self._pumped_at is None else round(time.monotonic() - self._pumped_at, 3)

    def start(self):
        listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        else:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind((self.host, self.port))
        listener.listen(4)
        listener.settimeout(0.5)
        self._listener = listener
        self._running = True
        self._thread = threading.Thread(target=self._accept_loop, name="snail-bridge-accept", daemon=True)
        self._thread.start()
        bpy.app.timers.register(self._timer, first_interval=PUMP_INTERVAL_SECONDS, persistent=True)

    def stop(self):
        self._running = False
        if bpy.app.timers.is_registered(self._timer):
            bpy.app.timers.unregister(self._timer)
        self._drop_clients()
        if self._listener is not None:
            self._listener.close()
            self._listener = None

    def pump(self):
        """Answers the queued requests on the main thread; whatever goes wrong with one of them, the next tick still comes.

        A bpy.app.timers callback that raises is never called again, and the loop of a Blender without an interface would end with it.
        """
        self._pumped_at = time.monotonic()
        try:
            return self._pump()
        except Exception:
            traceback.print_exc()
            return PUMP_INTERVAL_SECONDS

    def _accept_loop(self):
        while self._running:
            try:
                accepted, _ = self._listener.accept()
            except TimeoutError:
                continue
            except OSError:
                return
            with self._client_lock:
                self._drop_the_silent()
                room = len(self._clients) < MOST_CLIENTS
            if not room:
                with contextlib.suppress(OSError):
                    accepted.close()
                continue
            connection = Connection(accepted, self._receive, self._forget)
            with self._client_lock:
                self._clients.add(connection)
            connection.start()

    def _drop_the_silent(self):
        """Closes connections that have asked nothing since they arrived, before counting how many there are.

        Two threads, a socket and a queue go to every peer that connects, and the token is only read per request — so anything that can
        reach the port could take them by connecting and saying nothing. A client that has said nothing for half a minute is not a client
        of this add-on, and one whose host died without a FIN says nothing for ever.
        """
        for connection in [held for held in self._clients if held.is_silent(SILENT_CLIENT_SECONDS)]:
            connection.close()
            self._clients.discard(connection)

    def _forget(self, connection):
        with self._client_lock:
            self._clients.discard(connection)

    def _receive(self, connection, line):
        if line is None:
            self._send(connection, self._failure(None, "RequestTooLarge", f"a request above {MAX_LINE_BYTES} bytes was dropped"))
            return
        if not line.strip():
            return
        try:
            request = json.loads(line)
        except (ValueError, RecursionError) as error:
            self._send(connection, self._failure(None, "BadJson", str(error)))
            return
        if not isinstance(request, dict):
            self._send(connection, self._failure(None, "BadRequest", "a request is a JSON object"))
            return
        request_id = request.get("id")
        command = request.get("command")
        if not isinstance(command, str):
            self._send(connection, self._failure(request_id, "BadRequest", "a request needs a string 'command'"))
            return
        if not self._is_trusted(request):
            self._send(connection, self._failure(request_id, "Unauthorized", "the add-on expects the token set in its preferences; configure the same one on the server"))
            connection.finish()
            return
        if command in self._immediate:
            self._send(connection, self._answer(request, lambda params: self._immediate[command](self, params)))
            return
        try:
            self._pending.put_nowait((connection, request, _expires_at(request)))
        except queue.Full:
            self._send(connection, self._failure(request_id, "Busy", f"{MAX_QUEUED_REQUESTS} requests are already waiting for Blender's main thread; retry after one finishes"))

    def _is_trusted(self, request):
        if not self._token:
            return True
        offered = str(request.get("token") or "").encode("utf-8", errors="replace")
        return hmac.compare_digest(offered, self._token.encode("utf-8", errors="replace"))

    def _pump(self):
        while True:
            try:
                connection, request, expires_at = self._pending.get_nowait()
            except queue.Empty:
                return PUMP_INTERVAL_SECONDS
            if not connection.is_open:
                continue
            if expires_at is not None and time.monotonic() > expires_at:
                self._send(connection, self._failure(request.get("id"), "Expired", "the caller stopped waiting before Blender's main thread reached this request; it was not run"))
                continue
            self._executing = request.get("command")
            try:
                reply = self._answer(request, functools.partial(self._dispatch, request["command"]))
            finally:
                self._executing = None
            self._send(connection, reply)

    def _answer(self, request, run):
        request_id = request.get("id")
        params = request.get("params") or {}
        if not isinstance(params, dict):
            return self._failure(request_id, "BadRequest", "'params' must be an object")
        try:
            return {"id": request_id, "ok": True, "result": run(params)}
        except CommandError as error:
            return self._failure(request_id, error.kind, str(error), error.details)
        except Exception as error:
            trace = traceback.format_exc().splitlines()[-TRACEBACK_LINES:]
            return self._failure(request_id, type(error).__name__, str(error), {"traceback": trace})

    @staticmethod
    def _failure(request_id, kind, message, details=None):
        error = {"type": kind, "message": message}
        if details:
            error["details"] = details
        return {"id": request_id, "ok": False, "error": error}

    @classmethod
    def _encode(cls, reply):
        """One line of UTF-8 whatever the reply holds: a lone surrogate, which is how Python carries a file name that is not UTF-8, becomes '?'."""
        try:
            payload = json.dumps(reply, ensure_ascii=False, default=str)
        except (TypeError, ValueError, RecursionError) as error:
            payload = json.dumps(cls._failure(reply.get("id"), "Unserializable", str(error)))
        data = payload.encode("utf-8", errors="replace")
        if len(data) > MAX_REPLY_BYTES:
            data = json.dumps(cls._failure(
                reply.get("id"), "ReplyTooLarge",
                f"the reply is {len(data)} bytes against a cap of {MAX_REPLY_BYTES}; ask for less")).encode("utf-8")
        return data + b"\n"

    def _send(self, connection, reply):
        connection.send(self._encode(reply))

    def _drop_clients(self):
        with self._client_lock:
            clients, self._clients = list(self._clients), set()
        for client in clients:
            client.close()
