// WebGLビルドをローカルで確認するための簡易Webサーバー(Node.js標準機能のみ・外部パッケージ不要)。
// 使い方:  node Tools/serve-webgl.js  → ブラウザで http://localhost:8080 を開く
// ビルドは gzip+ブラウザ側展開(decompressionFallback)なので、特別なヘッダー設定は不要。
const http = require("http");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..", "Build", "WebGL");
const port = Number(process.env.PORT || 8080);
const types = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript",
  ".wasm": "application/wasm",
  ".data": "application/octet-stream",
  ".unityweb": "application/octet-stream",
  ".css": "text/css",
  ".png": "image/png",
  ".ico": "image/x-icon",
  ".json": "application/json",
};

http
  .createServer((req, res) => {
    const urlPath = decodeURIComponent(req.url.split("?")[0]);
    let file = path.join(root, urlPath === "/" ? "index.html" : urlPath);
    if (!file.startsWith(root)) {
      res.writeHead(403);
      return res.end();
    }
    fs.readFile(file, (err, data) => {
      if (err) {
        res.writeHead(404);
        return res.end("not found");
      }
      res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream" });
      res.end(data);
    });
  })
  .listen(port, "127.0.0.1", () => console.log(`serving ${root} at http://localhost:${port}`));
