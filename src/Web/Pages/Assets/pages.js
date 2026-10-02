// The pages work without this: it asks before a deletion, which nothing here can undo, ticks every box at once,
// and shows the times the server writes in UTC in the reader's own zone.
(function () {
  function size(bytes) {
    var units = ['B', 'KB', 'MB', 'GB', 'TB'];
    var value = bytes;
    var unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
      value /= 1024;
      unit++;
    }
    return unit === 0 ? bytes + ' B' : value.toFixed(value >= 100 ? 0 : 1).replace(/\.0$/, '') + ' ' + units[unit];
  }

  document.addEventListener('submit', function (event) {
    var button = event.submitter;
    if (!button || !button.dataset.confirm) {
      return;
    }
    var question;
    if (button.dataset.confirm === 'one') {
      question = 'Delete ' + button.value + ', ' + size(Number(button.dataset.bytes || 0)) + '? This cannot be undone.';
    } else if (button.dataset.confirm === 'ended') {
      question = 'Delete every job here that has ended, with its files? This cannot be undone.';
    } else {
      var boxes = event.target.querySelectorAll('input[name="names"]:checked');
      if (boxes.length === 0) {
        event.preventDefault();
        window.alert('Select what to delete first.');
        return;
      }
      var bytes = 0;
      boxes.forEach(function (box) { bytes += Number(box.dataset.bytes || 0); });
      question = 'Delete ' + boxes.length + (boxes.length === 1 ? ' item, ' : ' items, ') + size(bytes) + '? This cannot be undone.';
    }
    if (!window.confirm(question)) {
      event.preventDefault();
    }
  });

  document.querySelectorAll('[data-select-all]').forEach(function (all) {
    all.addEventListener('change', function () {
      all.form.querySelectorAll('input[name="names"]').forEach(function (box) {
        if (!box.disabled) {
          box.checked = all.checked;
        }
      });
    });
  });

  document.querySelectorAll('time[datetime]').forEach(function (time) {
    if (!time.dateTime) {
      return;
    }
    time.textContent = new Date(time.dateTime).toLocaleString([], { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
  });
})();
