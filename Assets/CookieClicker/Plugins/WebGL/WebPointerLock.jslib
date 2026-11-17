// 브라우저가 지금 실제로 마우스를 잠그고 있는지, 그리고 사용자가 Esc로 잠금을 풀었는지 알려 준다.
// Unity의 Cursor.lockState는 브라우저가 잠금을 풀어도(창 밖 클릭, Alt+Tab, Esc) Locked로 남을 수 있어서 직접 물어본다.
mergeInto(LibraryManager.library, {
  CookieWeb_IsPointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  },

  // 마우스가 잠긴 동안 누른 Esc는 브라우저가 잠금 해제에 써 버리고 게임에는 보내지 않는다.
  // 그래서 "창에 초점이 그대로인데 게임이 풀지 않은 잠금이 풀렸다"면 Esc를 누른 것으로 보고 한 번 알려 준다.
  // (게임이 스스로 푼 경우, 창 밖 클릭·Alt+Tab으로 초점이 나간 경우, 진짜 Esc 키가 게임에 들어온 경우는 빼고)
  CookieWeb_ConsumeEscUnlock: function () {
    var state = Module.cookieEscState;
    if (!state) {
      state = Module.cookieEscState = { pending: false, lastEscKey: -1e9, lastSelfExit: -1e9 };
      window.addEventListener("keydown", function (e) {
        if (e.key === "Escape") state.lastEscKey = performance.now();
      }, true);
      var exit = Document.prototype.exitPointerLock;
      if (exit) {
        Document.prototype.exitPointerLock = function () {
          state.lastSelfExit = performance.now();
          return exit.apply(this, arguments);
        };
      }
      document.addEventListener("pointerlockchange", function () {
        if (document.pointerLockElement || !document.hasFocus()) return;
        var at = performance.now();
        if (at - state.lastSelfExit < 300) return;
        // 초점이 나가는 이벤트가 잠금 해제보다 조금 늦게 올 수도 있어서 잠깐 기다렸다가 판단한다.
        setTimeout(function () {
          if (document.hasFocus() && Math.abs(state.lastEscKey - at) > 250) state.pending = true;
        }, 120);
      });
    }
    if (!state.pending) return 0;
    state.pending = false;
    return 1;
  }
});
