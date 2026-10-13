mergeInto(LibraryManager.library, {
  // Copies text to the browser clipboard. If the browser refuses, the text is shown in a prompt to copy by hand.
  RewindClipboardCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function showForManualCopy() { window.prompt("Copy this level code:", text); }
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(showForManualCopy);
      else showForManualCopy();
    } catch (e) {
      showForManualCopy();
    }
  },

  // Asks the player to paste text. Returns an empty string if they cancel.
  RewindClipboardPrompt: function (messagePtr, defaultPtr) {
    var result = window.prompt(UTF8ToString(messagePtr), UTF8ToString(defaultPtr));
    if (result === null) result = "";
    var size = lengthBytesUTF8(result) + 1;
    var buffer = _malloc(size);
    stringToUTF8(result, buffer, size);
    return buffer;
  }
});
