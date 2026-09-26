// Browser WebSocket bridge for WebGLWsTransport.cs. Incoming messages are
// queued per socket and pulled by C# each frame (no callbacks into C#).
var WormsSocketLib = {
  $WormsWs: { next: 1, sockets: {} },

  WormsWs_Open: function (urlPtr) {
    var id = WormsWs.next++;
    var entry = { ws: null, queue: [] };
    try {
      entry.ws = new WebSocket(UTF8ToString(urlPtr));
      entry.ws.binaryType = 'arraybuffer';
      entry.ws.onmessage = function (e) {
        if (e.data instanceof ArrayBuffer) entry.queue.push(new Uint8Array(e.data));
      };
    } catch (err) {
      console.error('WormsWs_Open', err);
    }
    WormsWs.sockets[id] = entry;
    return id;
  },

  // 0 connecting, 1 open, 2 closing, 3 closed (same as WebSocket.readyState).
  WormsWs_State: function (id) {
    var entry = WormsWs.sockets[id];
    if (!entry || !entry.ws) return 3;
    return entry.ws.readyState;
  },

  WormsWs_PeekSize: function (id) {
    var entry = WormsWs.sockets[id];
    if (!entry || entry.queue.length === 0) return -1;
    return entry.queue[0].length;
  },

  WormsWs_Pop: function (id, bufferPtr, length) {
    var entry = WormsWs.sockets[id];
    if (!entry || entry.queue.length === 0) return;
    var msg = entry.queue.shift();
    HEAPU8.set(msg.subarray(0, length), bufferPtr);
  },

  WormsWs_Send: function (id, dataPtr, length) {
    var entry = WormsWs.sockets[id];
    if (!entry || !entry.ws || entry.ws.readyState !== 1) return;
    entry.ws.send(HEAPU8.slice(dataPtr, dataPtr + length));
  },

  WormsWs_Close: function (id) {
    var entry = WormsWs.sockets[id];
    if (!entry) return;
    try { if (entry.ws) entry.ws.close(); } catch (err) {}
    delete WormsWs.sockets[id];
  },
};

autoAddDeps(WormsSocketLib, '$WormsWs');
mergeInto(LibraryManager.library, WormsSocketLib);
