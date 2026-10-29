mergeInto(LibraryManager.library, {
  WebClothReport: function (jsonPointer) {
    var result = JSON.parse(UTF8ToString(jsonPointer));
    window.webClothTest = result;
    document.title = 'Unity Web Cloth';
    var status = document.getElementById('web-cloth-status');
    if (!status) {
      status = document.createElement('div');
      status.id = 'web-cloth-status';
      status.setAttribute('role', 'status');
      status.style.cssText = 'position:fixed;left:12px;right:12px;top:12px;padding:10px;background:#111827dd;color:white;font:13px system-ui;pointer-events:none;white-space:pre-wrap';
      document.body.appendChild(status);
    }
    status.dataset.result = JSON.stringify(result);
    var number = function (value) { return value > 0 ? value.toFixed(1) : '—'; };
    status.textContent = result.done
      ? (result.success ? 'PASS' : 'FAIL') + ' · 81-particle cloth · ' + result.backend + ' · ' + result.graphics + '\n' + result.message + '\n' + (result.dragging ? 'DRAGGING' : result.moving ? 'MOVING' : 'IDLE') + ' · grabs ' + result.grabs + '\nRecent frame avg ' + number(result.averageFrameMs) + ' ms · p95 ' + number(result.p95FrameMs) + ' · worst ' + number(result.worstFrameMs) + '\nIdle avg ' + number(result.idleAverageMs) + ' ms · drag avg ' + number(result.dragAverageMs) + ' ms\nWhole player frames; desktop test is not a phone benchmark.'
      : result.message;
  }
});
