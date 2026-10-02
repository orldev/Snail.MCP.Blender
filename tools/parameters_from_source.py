"""Which parameter keys every bridge command reads, taken from the add-on's own source, and whether commands.json says the same.

The add-on refuses a parameter it does not read (addon/parameters.py), and it learns which those are from the "params" list in
addon/commands.json. That list is therefore a second copy of something the handlers already say, and a second copy drifts: a handler
that starts reading a new key would have callers refused for sending it. So this tool reads the keys out of the source and either
writes them into the schema (--write) or fails when the two disagree, which is what CI runs.

It reads a second thing the same way: which of those parameters name an object or a collection. The lease guard has to know
what a command will touch before it touches it, and a list of likely key names — name, target, objects and their kin — misses
a name inside a block (focus.object), a name in a key of its own (pivot_name, bevel_object) and a name that is the key of a
block (object_motion_blur). So the value is followed instead: every name reaches Blender through _object, _mesh_object or
bpy.data.{objects,collections}.get, and what matters is the parameter it came from. A path is the parameter's key, with '.'
for a key inside a block and '[]' for each item of a list along the way; at the end of a path a string is taken as it is, a
list gives its strings and a block gives its keys.

A handler rarely reads params itself: it hands the dict to _require, _scene, _vector and their kin, so the keys
live one or two calls deeper, and some are computed from a constant table rather than written out. So every
function is summarised as "for parameter i, these keys are read", where a key is either a literal or "whatever
parameter j holds", and the summaries are resolved against each other until they stop changing. Constants are
evaluated as far as they go: an index nobody can predict yields every value the table could have given, which
is wider than the truth and never narrower. A key that stays unknown makes the command open, and an open
command is a hole in the guard rather than a detail to wave through.
"""

import ast
import json
import os
import sys
from collections import defaultdict

READERS = ("get", "pop", "setdefault")
UNKNOWN = object()
OBJECTS = "objects"
COLLECTIONS = "collections"


class Any:
    """Every value a place could hold, when the source does not say which one it holds."""

    def __init__(self, members):
        self.members = [member for member in members if member is not UNKNOWN]
        self.incomplete = len(self.members) != len(list(members))

    def __iter__(self):
        return iter(self.members)


def flatten(value):
    """One value, or every value an Any stands for, however deeply they nest."""
    if not isinstance(value, Any):
        return [value]
    spread = []
    for member in value:
        spread.extend(flatten(member))
    return spread


def elements(value):
    """What a loop over this value walks, one element per iteration."""
    walked = []
    if isinstance(value, Walked):
        return Any(flatten(value)) if not any(isinstance(one, tuple) for one in value.members) else value
    for one in flatten(value):
        if isinstance(one, (tuple, list)):
            walked.extend(one)
        elif isinstance(one, dict):
            walked.extend(one.keys())
        elif one is UNKNOWN:
            return UNKNOWN
        else:
            walked.append(one)
    return Any(walked)


class Walked(Any):
    """An Any whose members are already the elements of a loop, such as a mapping's items()."""


class AddOn:
    def __init__(self, folder):
        self.trees = {}
        for entry in sorted(os.listdir(folder)):
            if entry.endswith(".py"):
                with open(os.path.join(folder, entry), encoding="utf-8") as handle:
                    self.trees[entry[:-3]] = ast.parse(handle.read(), filename=entry)
        self.imports = defaultdict(dict)
        self.functions = {}
        self.constants = {}
        for module, tree in self.trees.items():
            for node in tree.body:
                if isinstance(node, ast.ImportFrom) and node.level:
                    for alias in node.names:
                        self.imports[module][alias.asname or alias.name] = alias.name
                elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    self.functions[(module, node.name)] = node
                elif isinstance(node, ast.Assign):
                    try:
                        value = ast.literal_eval(node.value)
                    except (ValueError, SyntaxError):
                        continue
                    for target in node.targets:
                        if isinstance(target, ast.Name):
                            self.constants[(module, target.id)] = value

    def function(self, module, name):
        if (module, name) in self.functions:
            return (module, name)
        if name in self.imports[module] or True:
            for other in [module, *self.trees]:
                if (other, name) in self.functions:
                    return (other, name)
        return None

    def constant(self, module, name):
        for other in [module, *self.trees]:
            if (other, name) in self.constants:
                return self.constants[(other, name)]
        return UNKNOWN

    def commands(self):
        found = {}
        for module, tree in self.trees.items():
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    for decorator in node.decorator_list:
                        if (isinstance(decorator, ast.Call) and isinstance(decorator.func, ast.Name)
                                and decorator.func.id in ("command", "immediate_command")
                                and decorator.args and isinstance(decorator.args[0], ast.Constant)):
                            found[decorator.args[0].value] = (module, node.name, decorator.func.id)
        return found


class Reads(ast.NodeVisitor):
    """The keys one function reads out of each of its dict parameters, and the calls that carry a dict deeper."""

    def __init__(self, addon, module, name, summary, calls):
        self.addon = addon
        self.module = module
        self.name = name
        self.summary = summary
        self.calls = calls
        self.params = {argument.arg: index for index, argument in enumerate(addon.functions[(module, name)].args.args)}
        self.values = {}

    # -- values ------------------------------------------------------------
    def evaluate(self, node):
        if isinstance(node, ast.Constant):
            return node.value
        if isinstance(node, ast.Name):
            if node.id in self.values:
                return self.values[node.id]
            return self.addon.constant(self.module, node.id)
        if isinstance(node, (ast.Tuple, ast.List)):
            return tuple(self.evaluate(item) for item in node.elts)
        if isinstance(node, ast.Dict):
            if any(key is None for key in node.keys):  # {**other} spreads a mapping this reading cannot follow
                return UNKNOWN
            pairs = [(self.evaluate(node.keys[place]), self.evaluate(node.values[place])) for place in range(len(node.keys))]
            return UNKNOWN if any(key is UNKNOWN for key, _ in pairs) else dict(pairs)
        if isinstance(node, ast.IfExp):
            return Any([self.evaluate(node.body), self.evaluate(node.orelse)])
        if isinstance(node, ast.JoinedStr):
            pieces = [[part.value] if isinstance(part, ast.Constant) else flatten(self.evaluate(part.value)) for part in node.values]
            if any(UNKNOWN in piece or not piece for piece in pieces):
                return UNKNOWN
            grown = [""]
            for piece in pieces:
                grown = [head + str(tail) for head in grown for tail in piece]
            return Any(grown) if len(grown) > 1 else grown[0]
        if isinstance(node, ast.Subscript):
            container = self.evaluate(node.value)
            index = self.evaluate(node.slice)
            return self.index(container, index)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr in ("items", "keys", "values", "upper", "lower"):
            container = self.evaluate(node.func.value)
            if node.func.attr in ("upper", "lower") or container is UNKNOWN:
                return UNKNOWN
            return Walked(flatten(self.view(container, node.func.attr)))
        return UNKNOWN

    def index(self, container, index):
        if container is UNKNOWN:
            return UNKNOWN
        options = []
        for one in flatten(container):
            if isinstance(one, dict):
                options.extend(one.values() if index is UNKNOWN or isinstance(index, Any) else [one.get(index, UNKNOWN)])
            elif isinstance(one, (tuple, list)):
                options.extend(one if index is UNKNOWN or isinstance(index, Any) else
                               [one[index] if isinstance(index, int) and -len(one) <= index < len(one) else UNKNOWN])
            else:
                options.append(UNKNOWN)
        return Any(options) if len(options) != 1 else options[0]

    def view(self, container, attribute):
        """What a loop over .items(), .keys() or .values() walks: one element per iteration, merged across tables."""
        elements = []
        for one in flatten(container):
            if not isinstance(one, dict):
                return UNKNOWN
            if attribute == "keys":
                elements.extend(one.keys())
            elif attribute == "values":
                elements.extend(one.values())
            else:
                elements.extend(one.items())
        return Any(elements)

    # -- bindings ----------------------------------------------------------
    def bind(self, target, value):
        if isinstance(target, ast.Name):
            self.values[target.id] = value
        elif isinstance(target, ast.Tuple):
            for position, element in enumerate(target.elts):
                self.bind(element, self.index(value, position))

    def visit_Assign(self, node):
        value = self.evaluate(node.value)
        if value is not UNKNOWN:
            for target in node.targets:
                self.bind(target, value)
        self.generic_visit(node)

    def visit_For(self, node):
        iterable = elements(self.evaluate(node.iter))
        if iterable is not UNKNOWN:
            self.bind(node.target, iterable)
        self.generic_visit(node)

    def comprehension(self, node):
        for generator in node.generators:
            iterable = elements(self.evaluate(generator.iter))
            if iterable is not UNKNOWN:
                self.bind(generator.target, iterable)
        self.generic_visit(node)

    visit_ListComp = visit_SetComp = visit_GeneratorExp = visit_DictComp = comprehension

    # -- reads -------------------------------------------------------------
    def key_of(self, node, sink):
        value = self.evaluate(node)
        names = [one for one in flatten(value) if isinstance(one, str)]
        if value is UNKNOWN or not names or len(names) != len(flatten(value)):
            if isinstance(node, ast.Name) and node.id in self.params:
                sink.add(("arg", self.params[node.id]))
                return
            sink.add(("open", ast.dump(node)[:70]))
            return
        for name in names:
            sink.add(("lit", name))

    def visit_Subscript(self, node):
        if isinstance(node.value, ast.Name) and node.value.id in self.params:
            self.key_of(node.slice, self.summary[(self.module, self.name)][self.params[node.value.id]])
        self.generic_visit(node)

    def carried(self, node):
        """Which dict parameter this argument carries: itself, or a dict built out of its items."""
        if isinstance(node, ast.Name) and node.id in self.params:
            return self.params[node.id]
        if isinstance(node, (ast.DictComp, ast.Dict)):
            walked = node.generators[0].iter if isinstance(node, ast.DictComp) else None
            if (isinstance(walked, ast.Call) and isinstance(walked.func, ast.Attribute) and walked.func.attr == "items"
                    and isinstance(walked.func.value, ast.Name) and walked.func.value.id in self.params):
                return self.params[walked.func.value.id]
        if (isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "dict"
                and len(node.args) == 1 and isinstance(node.args[0], ast.Name) and node.args[0].id in self.params):
            return self.params[node.args[0].id]
        return None

    def visit_Call(self, node):
        if (isinstance(node.func, ast.Attribute) and node.func.attr in READERS
                and isinstance(node.func.value, ast.Name) and node.func.value.id in self.params and node.args):
            self.key_of(node.args[0], self.summary[(self.module, self.name)][self.params[node.func.value.id]])
        if isinstance(node.func, ast.Name):
            target = self.addon.function(self.module, node.func.id)
            if target is not None:
                for index, argument in enumerate(node.args):
                    carried = self.carried(argument)
                    if carried is not None:
                        self.calls.append((self.module, self.name, carried, target, index, node, self))
        self.generic_visit(node)


class Paths(ast.NodeVisitor):
    """Where each value in one function came from, as a path into one of its dict parameters."""

    def __init__(self, addon, module, name, resolves, found, calls):
        self.addon = addon
        self.module = module
        self.name = name
        self.resolves = resolves            # (module, name) -> {arg index: kind}
        self.found = found                  # (module, name) -> {param index: {(kind, path)}}
        self.calls = calls
        self.params = {argument.arg: index for index, argument in enumerate(addon.functions[(module, name)].args.args)}
        self.paths = {}                     # local name -> {(param index, path)}
        self.items = {}                     # local name -> the parameter this value is an item of

    # -- where a value came from -------------------------------------------
    def paths_of(self, node):
        """Every parameter path this value may have come from; several when the source offers a choice."""
        if isinstance(node, ast.Name):
            return list(self.paths.get(node.id, ()))
        if isinstance(node, ast.Subscript) and isinstance(node.slice, ast.Constant) and isinstance(node.slice.value, str):
            return self.grow(node.value, node.slice.value)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr in ("get", "pop", "setdefault"):
            if node.args and isinstance(node.args[0], ast.Constant) and isinstance(node.args[0].value, str):
                return self.grow(node.func.value, node.args[0].value)
            return []
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "_require":
            if len(node.args) > 1 and isinstance(node.args[1], ast.Constant) and isinstance(node.args[1].value, str):
                return self.grow(node.args[0], node.args[1].value)
            return []
        if isinstance(node, ast.BoolOp):  # params.get("camera") or params.get("name") — whichever is there
            return [path for value in node.values for path in self.paths_of(value)]
        if isinstance(node, (ast.List, ast.Tuple)):  # [params["target"]] — a list made of one parameter is that parameter
            return [path for item in node.elts for path in self.paths_of(item)]
        if isinstance(node, ast.IfExp):
            return self.paths_of(node.body) + self.paths_of(node.orelse)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in ("str", "list", "sorted"):
            return self.paths_of(node.args[0]) if node.args else []
        return []

    def grow(self, container, key):
        """A key read out of something: the parameter itself, or a block already inside it."""
        if isinstance(container, ast.Name) and container.id in self.params:
            return [(self.params[container.id], key)]
        return [(index, f"{path}.{key}") for index, path in self.paths_of(container)]

    def bind(self, target, paths):
        if paths and isinstance(target, ast.Name):
            self.paths.setdefault(target.id, set()).update(paths)

    def visit_Assign(self, node):
        paths = self.paths_of(node.value)
        for target in node.targets:
            self.bind(target, paths)
        self.generic_visit(node)

    def visit_For(self, node):
        walked = node.iter
        keys = False
        if isinstance(walked, ast.Call) and isinstance(walked.func, ast.Attribute) and walked.func.attr in ("items", "keys"):
            keys, walked = True, walked.func.value
        target = node.target
        if isinstance(target, ast.Tuple) and target.elts:
            target = target.elts[0]
        self.bind(target, [(index, path + ("{}" if keys else "[]")) for index, path in self.paths_of(walked)])
        self.item_of(target, walked)
        self.generic_visit(node)

    def item_of(self, target, walked):
        """A loop over a parameter itself, with no path to it: whatever the body makes of an item, the parameter is a list of."""
        if isinstance(target, ast.Name) and isinstance(walked, ast.Name) and walked.id in self.params:
            self.items[target.id] = self.params[walked.id]

    def comprehension(self, node):
        for generator in node.generators:
            walked, keys = generator.iter, False
            if isinstance(walked, ast.Call) and isinstance(walked.func, ast.Attribute) and walked.func.attr in ("items", "keys"):
                keys, walked = True, walked.func.value
            target = generator.target
            if isinstance(target, ast.Tuple) and target.elts:
                target = target.elts[0]
            self.bind(target, [(index, path + ("{}" if keys else "[]")) for index, path in self.paths_of(walked)])
            self.item_of(target, walked)
        self.generic_visit(node)

    visit_ListComp = visit_SetComp = visit_GeneratorExp = visit_DictComp = comprehension

    # -- where a value goes ------------------------------------------------
    def note(self, kind, node):
        """This expression is used as the name of an object or a collection."""
        paths = self.paths_of(node)
        for index, path in paths:
            self.found[(self.module, self.name)][index].add((kind, path))
        if not paths and isinstance(node, ast.Name):
            if node.id in self.params:
                self.resolves[(self.module, self.name)][self.params[node.id]] = kind
            elif node.id in self.items:
                self.resolves[(self.module, self.name)][self.items[node.id]] = kind

    def visit_Subscript(self, node):
        if isinstance(node.value, ast.Attribute) and _is_data(node.value, OBJECTS):
            self.note(OBJECTS, node.slice)
        if isinstance(node.value, ast.Attribute) and _is_data(node.value, COLLECTIONS):
            self.note(COLLECTIONS, node.slice)
        self.generic_visit(node)

    def visit_Compare(self, node):
        """item.name in params["names"] — objects chosen by their name, without a lookup to follow."""
        if any(isinstance(operator, ast.In) for operator in node.ops) and isinstance(node.left, ast.Attribute) and node.left.attr == "name":
            self.note(OBJECTS, node.comparators[0])
        self.generic_visit(node)

    def visit_Call(self, node):
        if isinstance(node.func, ast.Attribute) and node.func.attr in ("get",) and node.args:
            for kind in (OBJECTS, COLLECTIONS):
                if _is_data(node.func.value, kind):
                    self.note(kind, node.args[0])
        if isinstance(node.func, ast.Name):
            target = self.addon.function(self.module, node.func.id)
            if target is not None:
                for index, argument in enumerate(node.args):
                    self.calls.append((self.module, self.name, target, index, argument, self))
        self.generic_visit(node)


def _is_data(node, collection):
    """bpy.data.objects or bpy.data.collections, however it is spelled."""
    return (isinstance(node, ast.Attribute) and node.attr == collection
            and isinstance(node.value, ast.Attribute) and node.value.attr == "data")

def read(folder):
    addon = AddOn(folder)
    summary = defaultdict(lambda: defaultdict(set))
    calls = []
    for module, name in list(addon.functions):
        Reads(addon, module, name, summary, calls).visit(addon.functions[(module, name)])
    for _ in range(16):
        changed = False
        for module, name, param_index, target, arg_index, call, visitor in calls:
            grown = set()
            for tag, value in set(summary[target][arg_index]):
                if tag != "arg":
                    grown.add((tag, value))
                elif value < len(call.args):
                    visitor.key_of(call.args[value], grown)
                else:
                    callee = addon.functions[target]
                    offset = len(callee.args.args) - len(callee.args.defaults)
                    if value >= offset:
                        visitor.key_of(callee.args.defaults[value - offset], grown)
            sink = summary[(module, name)][param_index]
            if not grown <= sink:
                sink |= grown
                changed = True
        if not changed:
            break
    report = {}
    for name, (module, function, kind) in sorted(addon.commands().items()):
        index = 1 if kind == "immediate_command" else 0
        descriptors = summary[(module, function)][index]
        report[name] = {
            "module": module,
            "keys": sorted({value for tag, value in descriptors if tag == "lit"}),
            "open": sorted({value for tag, value in descriptors if tag == "open"}),
        }
    return report


def names(folder, addon=None):
    """Per command, the paths of its parameters that name an object or a collection."""
    addon = addon or AddOn(folder)
    resolves = defaultdict(dict)
    found = defaultdict(lambda: defaultdict(set))
    calls = []
    for module, name in list(addon.functions):
        Paths(addon, module, name, resolves, found, calls).visit(addon.functions[(module, name)])

    def weight():
        return sum(len(one) for one in found.values() for one in one.values()), sum(len(one) for one in resolves.values())

    for _ in range(16):
        before = weight()
        for module, name, target, index, argument, visitor in calls:
            kind = resolves[target].get(index)
            if kind is not None:  # the callee uses this argument as a name
                visitor.note(kind, argument)
            if isinstance(argument, ast.Name) and argument.id in visitor.params:  # the callee finds names inside a dict this argument carries
                found[(module, name)][visitor.params[argument.id]] |= found[target][index]
        if weight() == before:
            break

    report = {}
    for command, (module, function, kind) in sorted(addon.commands().items()):
        index = 1 if kind == "immediate_command" else 0
        report[command] = sorted({_terminal(path) for _, path in found[(module, function)][index]})
    return report


def _terminal(path):
    """A path as the guard reads it: the end of one is taken generously, so the marks that say how are dropped there."""
    return path[:-2] if path.endswith(("[]", "{}")) else path


def declared(schema):
    return {name: list(entry.get("params") or []) for name, entry in schema["commands"].items()}


def rewrite(path, schema, keys, named):
    """The schema as it is, with both readings replaced and written on one line each: they are data about a command, not structure to walk."""
    import collections

    commands = schema["commands"]
    for name, entry in commands.items():
        rebuilt = collections.OrderedDict()
        rebuilt["params"] = keys[name]
        if named[name]:
            rebuilt["targets"] = named[name]
        for key, value in entry.items():
            if key not in ("params", "targets"):
                rebuilt[key] = value
        commands[name] = rebuilt

    text = json.dumps(schema, ensure_ascii=False, indent=2)
    lines, folding = [], False
    for line in text.splitlines():
        if line.strip().startswith(('"params": [', '"targets": [')):
            lines.append(line.rstrip())
            folding = not line.rstrip().endswith("]")
            continue
        if folding:
            lines[-1] += " " + line.strip()
            folding = not line.strip().startswith("]")
            continue
        lines.append(line)
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines).replace('"params": [ ]', '"params": []') + "\n")


def main(arguments):
    import collections

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    folder = os.path.join(root, "addon")
    path = os.path.join(folder, "commands.json")
    with open(path, encoding="utf-8") as handle:
        schema = json.load(handle, object_pairs_hook=collections.OrderedDict)

    found = read(folder)
    keys = {name: entry["keys"] for name, entry in found.items()}
    opaque = {name: entry["open"] for name, entry in found.items() if entry["open"]}
    named = names(folder)

    faults = [f"{name}: registered by the add-on or described by the schema, but not both"
              for name in sorted(set(keys) ^ set(schema["commands"]))]
    faults += [f"{name}: a parameter key this reading cannot name, so the gate cannot guard it: {reasons[0]}"
               for name, reasons in sorted(opaque.items())]

    faults += [f"{name}: names an object through a parameter it does not declare: {', '.join(sorted(stray))}"
               for name, stray in ((name, {path.split('[')[0].split('.')[0] for path in paths} - set(keys[name])) for name, paths in named.items())
               if stray]

    if "--write" in arguments and not faults:
        rewrite(path, schema, keys, named)
        print(f"wrote the parameters of {len(keys)} commands, and the objects named by {len([one for one in named.values() if one])} of them, into addon/commands.json")
        return 0

    for name in sorted(set(keys) & set(schema["commands"])):
        missing = sorted(set(keys[name]) - set(declared(schema)[name]))
        extra = sorted(set(declared(schema)[name]) - set(keys[name]))
        if missing:
            faults.append(f"{name}: read by the handler, absent from commands.json, so callers are refused for sending it: {', '.join(missing)}")
        if extra:
            faults.append(f"{name}: declared in commands.json, read by nothing, so it is accepted and dropped in silence: {', '.join(extra)}")
        unseen = sorted(set(named[name]) - set(schema["commands"][name].get("targets") or []))
        invented = sorted(set(schema["commands"][name].get("targets") or []) - set(named[name]))
        if unseen:
            faults.append(f"{name}: names an object the lease guard is not told about: {', '.join(unseen)}")
        if invented:
            faults.append(f"{name}: declared as naming an object, and names none: {', '.join(invented)}")

    if faults:
        print("addon/commands.json no longer says what the handlers do:")
        for fault in faults:
            print(f"  {fault}")
        print("\nrun: python tools/parameters_from_source.py --write")
        return 1

    print(f"addon/commands.json declares exactly what the handlers read, for all {len(keys)} commands, "
          f"and the objects named by the {len([one for one in named.values() if one])} that name any")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
