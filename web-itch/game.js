(() => {
  "use strict";

  const DATA = window.GAME_LEVEL_DATA;
  if (!DATA) {
    throw new Error("GAME_LEVEL_DATA missing; ensure level-data.js is loaded.");
  }

  const VIRTUAL_W = 640;
  const VIRTUAL_H = 400;
  const UI_W = 1280;
  const UI_H = 800;
  const TILE_SIZE = DATA.tileSize;
  const WORLD_W = DATA.width * TILE_SIZE;
  const WORLD_H = DATA.height * TILE_SIZE;
  const FIXED_DT = 1 / 60;
  const SAVE_KEY = "escape_to_the_skies_save_v1";

  const canvas = document.getElementById("game");
  const ctx = canvas.getContext("2d", { alpha: false });
  ctx.imageSmoothingEnabled = false;

  const worldCanvas = document.createElement("canvas");
  worldCanvas.width = VIRTUAL_W;
  worldCanvas.height = VIRTUAL_H;
  const wctx = worldCanvas.getContext("2d", { alpha: false });
  wctx.imageSmoothingEnabled = false;

  // Tile helpers matching LevelData.cs
  const isSolid = (t) => t === 1 || t === 2 || t === 4 || t === 5 || t === 7;
  const isOneWay = (t) => t === 3 || t === 6 || t === 8;
  const isStandable = (t) => isSolid(t) || isOneWay(t);
  const getTile = (tx, ty) => {
    if (tx < 0 || tx >= DATA.width || ty < 0 || ty >= DATA.height) return 0;
    return DATA.tiles[ty * DATA.width + tx];
  };

  // =========================================================================
  // 5x7 CRISP BITMAP FONT (matches BitmapFont.cs)
  // =========================================================================
  const GLYPHS = {
    " ": [0x00,0x00,0x00,0x00,0x00,0x00,0x00],
    "!": [0x04,0x04,0x04,0x04,0x04,0x00,0x04],
    '"': [0x0A,0x0A,0x00,0x00,0x00,0x00,0x00],
    "#": [0x0A,0x1F,0x0A,0x0A,0x1F,0x0A,0x00],
    "$": [0x04,0x0F,0x14,0x0E,0x05,0x1E,0x04],
    "%": [0x18,0x19,0x02,0x04,0x08,0x13,0x03],
    "&": [0x0C,0x12,0x14,0x08,0x15,0x12,0x0D],
    "'": [0x04,0x04,0x08,0x00,0x00,0x00,0x00],
    "(": [0x02,0x04,0x08,0x08,0x08,0x04,0x02],
    ")": [0x08,0x04,0x02,0x02,0x02,0x04,0x08],
    "*": [0x00,0x04,0x15,0x0E,0x15,0x04,0x00],
    "+": [0x00,0x04,0x04,0x1F,0x04,0x04,0x00],
    ",": [0x00,0x00,0x00,0x00,0x04,0x04,0x08],
    "-": [0x00,0x00,0x00,0x1F,0x00,0x00,0x00],
    ".": [0x00,0x00,0x00,0x00,0x00,0x06,0x06],
    "/": [0x01,0x01,0x02,0x04,0x08,0x10,0x10],
    "0": [0x0E,0x11,0x13,0x15,0x19,0x11,0x0E],
    "1": [0x04,0x0C,0x04,0x04,0x04,0x04,0x0E],
    "2": [0x0E,0x11,0x01,0x02,0x04,0x08,0x1F],
    "3": [0x1F,0x02,0x04,0x02,0x01,0x11,0x0E],
    "4": [0x02,0x06,0x0A,0x12,0x1F,0x02,0x02],
    "5": [0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E],
    "6": [0x06,0x08,0x10,0x1E,0x11,0x11,0x0E],
    "7": [0x1F,0x01,0x02,0x04,0x08,0x08,0x08],
    "8": [0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E],
    "9": [0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C],
    ":": [0x00,0x06,0x06,0x00,0x06,0x06,0x00],
    ";": [0x00,0x06,0x06,0x00,0x06,0x04,0x08],
    "<": [0x02,0x04,0x08,0x10,0x08,0x04,0x02],
    "=": [0x00,0x00,0x1F,0x00,0x1F,0x00,0x00],
    ">": [0x08,0x04,0x02,0x01,0x02,0x04,0x08],
    "?": [0x0E,0x11,0x01,0x02,0x04,0x00,0x04],
    "@": [0x0E,0x11,0x17,0x15,0x17,0x10,0x0E],
    "A": [0x0E,0x11,0x11,0x1F,0x11,0x11,0x11],
    "B": [0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E],
    "C": [0x0E,0x11,0x10,0x10,0x10,0x11,0x0E],
    "D": [0x1C,0x12,0x11,0x11,0x11,0x12,0x1C],
    "E": [0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F],
    "F": [0x1F,0x10,0x10,0x1E,0x10,0x10,0x10],
    "G": [0x0E,0x11,0x10,0x17,0x11,0x11,0x0F],
    "H": [0x11,0x11,0x11,0x1F,0x11,0x11,0x11],
    "I": [0x0E,0x04,0x04,0x04,0x04,0x04,0x0E],
    "J": [0x07,0x02,0x02,0x02,0x02,0x12,0x0C],
    "K": [0x11,0x12,0x14,0x18,0x14,0x12,0x11],
    "L": [0x10,0x10,0x10,0x10,0x10,0x10,0x1F],
    "M": [0x11,0x1B,0x15,0x15,0x11,0x11,0x11],
    "N": [0x11,0x19,0x15,0x13,0x11,0x11,0x11],
    "O": [0x0E,0x11,0x11,0x11,0x11,0x11,0x0E],
    "P": [0x1E,0x11,0x11,0x1E,0x10,0x10,0x10],
    "Q": [0x0E,0x11,0x11,0x11,0x15,0x12,0x0D],
    "R": [0x1E,0x11,0x11,0x1E,0x14,0x12,0x11],
    "S": [0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E],
    "T": [0x1F,0x04,0x04,0x04,0x04,0x04,0x04],
    "U": [0x11,0x11,0x11,0x11,0x11,0x11,0x0E],
    "V": [0x11,0x11,0x11,0x11,0x11,0x0A,0x04],
    "W": [0x11,0x11,0x11,0x15,0x15,0x1B,0x11],
    "X": [0x11,0x11,0x0A,0x04,0x0A,0x11,0x11],
    "Y": [0x11,0x11,0x0A,0x04,0x04,0x04,0x04],
    "Z": [0x1F,0x01,0x02,0x04,0x08,0x10,0x1F],
    "[": [0x0E,0x08,0x08,0x08,0x08,0x08,0x0E],
    "]": [0x0E,0x02,0x02,0x02,0x02,0x02,0x0E],
    "_": [0x00,0x00,0x00,0x00,0x00,0x00,0x1F],
    "a": [0x00,0x00,0x0E,0x01,0x0F,0x11,0x0F],
    "b": [0x10,0x10,0x1E,0x11,0x11,0x11,0x1E],
    "c": [0x00,0x00,0x0E,0x10,0x10,0x11,0x0E],
    "d": [0x01,0x01,0x0F,0x11,0x11,0x11,0x0F],
    "e": [0x00,0x00,0x0E,0x11,0x1F,0x10,0x0E],
    "f": [0x06,0x09,0x08,0x1C,0x08,0x08,0x08],
    "g": [0x00,0x00,0x0F,0x11,0x0F,0x01,0x0E],
    "h": [0x10,0x10,0x1E,0x11,0x11,0x11,0x11],
    "i": [0x04,0x00,0x0C,0x04,0x04,0x04,0x0E],
    "j": [0x02,0x00,0x06,0x02,0x02,0x12,0x0C],
    "k": [0x10,0x10,0x12,0x14,0x18,0x14,0x12],
    "l": [0x0C,0x04,0x04,0x04,0x04,0x04,0x0E],
    "m": [0x00,0x00,0x1A,0x15,0x15,0x15,0x15],
    "n": [0x00,0x00,0x1E,0x11,0x11,0x11,0x11],
    "o": [0x00,0x00,0x0E,0x11,0x11,0x11,0x0E],
    "p": [0x00,0x00,0x1E,0x11,0x1E,0x10,0x10],
    "q": [0x00,0x00,0x0F,0x11,0x0F,0x01,0x01],
    "r": [0x00,0x00,0x16,0x19,0x10,0x10,0x10],
    "s": [0x00,0x00,0x0E,0x10,0x0E,0x01,0x1E],
    "t": [0x08,0x08,0x1C,0x08,0x08,0x09,0x06],
    "u": [0x00,0x00,0x11,0x11,0x11,0x13,0x0D],
    "v": [0x00,0x00,0x11,0x11,0x11,0x0A,0x04],
    "w": [0x00,0x00,0x11,0x11,0x15,0x15,0x0A],
    "x": [0x00,0x00,0x11,0x0A,0x04,0x0A,0x11],
    "y": [0x00,0x00,0x11,0x11,0x0F,0x01,0x0E],
    "z": [0x00,0x00,0x1F,0x02,0x04,0x08,0x1F],
    "|": [0x04,0x04,0x04,0x04,0x04,0x04,0x04],
  };

  function measureText(text, scale = 2) {
    if (!text) return { w: 0, h: 0 };
    const lines = text.split("\n");
    let maxCols = 0;
    for (const l of lines) maxCols = Math.max(maxCols, l.length);
    return {
      w: Math.max(0, maxCols * 6 - 1) * scale,
      h: (lines.length * 8 - 1) * scale,
    };
  }

  function drawRawText(targetCtx, text, x, y, color, scale) {
    targetCtx.fillStyle = color;
    let cx = Math.round(x);
    let cy = Math.round(y);
    for (let i = 0; i < text.length; i++) {
      const ch = text[i];
      if (ch === "\n") {
        cx = Math.round(x);
        cy += 8 * scale;
        continue;
      }
      const g = GLYPHS[ch] || GLYPHS["?"];
      for (let r = 0; r < 7; r++) {
        const bits = g[r];
        for (let c = 0; c < 5; c++) {
          if (bits & (1 << (4 - c))) {
            targetCtx.fillRect(cx + c * scale, cy + r * scale, scale, scale);
          }
        }
      }
      cx += 6 * scale;
    }
  }

  function drawText(targetCtx, text, x, y, color, scale = 2, shadow = true) {
    if (!text) return;
    if (shadow) {
      drawRawText(targetCtx, text, x + scale, y + scale, "rgba(10,10,18,0.78)", scale);
    }
    drawRawText(targetCtx, text, x, y, color, scale);
  }

  function drawCenteredText(targetCtx, text, centerX, y, color, scale = 2, shadow = true) {
    const s = measureText(text, scale);
    drawText(targetCtx, text, Math.round(centerX - s.w * 0.5), Math.round(y), color, scale, shadow);
  }

  function measureBadgeWidth(label, scale = 2) {
    return measureText(label, scale).w + 10 * scale;
  }

  function drawDeckBadge(targetCtx, label, x, y, color, scale = 2) {
    const sz = measureText(label, scale);
    const padX = 5 * scale;
    const padY = 2 * scale;
    const w = sz.w + padX * 2;
    const h = sz.h + padY * 2;
    targetCtx.fillStyle = "rgba(16,18,28,0.92)";
    targetCtx.fillRect(x, y, w, h);
    targetCtx.fillStyle = color;
    targetCtx.fillRect(x, y, w, scale);
    targetCtx.fillRect(x, y + h - scale, w, scale);
    targetCtx.fillRect(x, y, scale, h);
    targetCtx.fillRect(x + w - scale, y, scale, h);
    drawText(targetCtx, label, x + padX, y + padY, color, scale, true);
  }

  function drawBox(targetCtx, x, y, w, h, fill, border, thickness = 2) {
    targetCtx.fillStyle = fill;
    targetCtx.fillRect(x, y, w, h);
    targetCtx.fillStyle = border;
    targetCtx.fillRect(x, y, w, thickness);
    targetCtx.fillRect(x, y + h - thickness, w, thickness);
    targetCtx.fillRect(x, y, thickness, h);
    targetCtx.fillRect(x + w - thickness, y, thickness, h);
  }

  // =========================================================================
  // PROCEDURAL PIXEL ART ATLASES (matches PixelArtTextures.cs)
  // =========================================================================
  function makeCanvas(w, h) {
    const c = document.createElement("canvas");
    c.width = w;
    c.height = h;
    return c;
  }

  function buildTileAtlas() {
    const c = makeCanvas(9 * 16, 2 * 16);
    const g = c.getContext("2d");
    const img = g.createImageData(c.width, c.height);
    const px = img.data;
    const setPx = (x, y, col) => {
      if (!col) return;
      const i = (y * c.width + x) * 4;
      px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = col[3] ?? 255;
    };

    for (let row = 0; row < 2; row++) {
      const oy = row * 16;
      // 1 & 2: RuinStone & RuinMossStone
      for (const withMoss of [false, true]) {
        const ox = (withMoss ? 2 : 1) * 16;
        for (let y = 0; y < 16; y++) {
          for (let x = 0; x < 16; x++) {
            const border = x === 0 || y === 0 || x === 15 || y === 15;
            const seam = y === 7 || (y < 7 && x === 8) || (y > 7 && x === (row === 0 ? 4 : 12));
            let col = [44, 42, 74, 255];
            if (border || seam) col = [24, 22, 42, 255];
            else if (y === 1 || y === 8) col = [86, 86, 130, 255];
            else if ((x * 7 + y * 13 + row * 5) % 11 === 0) col = [62, 60, 98, 255];
            if (row === 1 && !withMoss && x >= 10 && x <= 12 && y >= 10 && y <= 12 && (x === 11 || y === 11)) {
              col = [53, 136, 150, 255];
            }
            if (withMoss) {
              const md = ((x + row * 3) % 5 === 0) ? 4 : (((x * 3) % 4 === 0) ? 2 : 3);
              if (y === 0) col = [62, 178, 146, 255];
              else if (y < md) col = (x % 3 === 0) ? [62, 178, 146, 255] : [26, 98, 92, 255];
              else if (y === md && x % 2 === 0) col = [24, 22, 42, 255];
            }
            setPx(ox + x, oy + y, col);
          }
        }
      }
      // 3: RuinPlatform
      {
        const ox = 3 * 16;
        for (let y = 0; y < 7; y++) {
          for (let x = 0; x < 16; x++) {
            let col = null;
            if (y === 0) col = (x === 0 || x === 15) ? [22, 20, 38] : [118, 120, 166];
            else if (y === 1) col = (x === 0 || x === 15) ? [22, 20, 38] : (x % 6 === 0 ? [118, 120, 166] : [74, 72, 114]);
            else if (y <= 4) {
              col = (x === 0 || x === 15 || x === 7 + row) ? [22, 20, 38] : [74, 72, 114];
              if (y === 3 && (x === 3 || x === 12)) col = [77, 156, 171];
            } else if (y === 5 && x >= 1 && x <= 14) col = [44, 42, 72];
            else if (y === 6 && x >= 2 && x <= 13 && x % 3 !== 0) col = [22, 20, 38];
            setPx(ox + x, oy + y, col);
          }
        }
      }
      // 4 & 5: ForestLog & ForestGrass
      for (const withGrass of [false, true]) {
        const ox = (withGrass ? 5 : 4) * 16;
        for (let y = 0; y < 16; y++) {
          for (let x = 0; x < 16; x++) {
            let col = [82, 54, 38];
            if (x === 0 || y === 0 || x === 15 || y === 15) col = [42, 28, 22];
            else if (((x + row * 2) % 4 === 0 && y % 5 !== 0) || y === 1) col = [114, 78, 52];
            else if ((x * 5 + y * 3) % 9 === 0) col = [42, 28, 22];
            if (withGrass) {
              const d = 3 + ((x * 3 + row) % 3);
              if (y === 0) col = [134, 218, 96];
              else if (y === 1) col = (x % 3 === 0) ? [134, 218, 96] : [76, 174, 74];
              else if (y < d) col = (x % 2 === 0) ? [76, 174, 74] : [38, 116, 56];
              else if (y === d) col = [42, 28, 22];
            }
            setPx(ox + x, oy + y, col);
          }
        }
      }
      // 6: ForestBranch
      {
        const ox = 6 * 16;
        for (let y = 0; y < 8; y++) {
          for (let x = 0; x < 16; x++) {
            let col = null;
            if (y === 0) col = (x % 4 === (row === 0 ? 1 : 2)) ? [138, 220, 96] : (x % 2 === 0 ? [72, 172, 74] : [144, 100, 62]);
            else if (y === 1) col = (x === 0 || x === 15) ? [34, 24, 18] : [144, 100, 62];
            else if (y <= 4) col = (x === 0 || x === 15) ? [34, 24, 18] : ((y === 3 && x % 5 === 0) ? [144, 100, 62] : [104, 68, 42]);
            else if (y === 5 && x >= 1 && x <= 14) col = [34, 24, 18];
            else if (y >= 6 && ((x >= 2 && x <= 5) || (x >= 10 && x <= 13))) col = y === 6 ? [72, 172, 74] : [34, 24, 18];
            setPx(ox + x, oy + y, col);
          }
        }
      }
      // 7: SkyMarble
      {
        const ox = 7 * 16;
        for (let y = 0; y < 16; y++) {
          for (let x = 0; x < 16; x++) {
            let col = [218, 234, 248];
            if (x === 0 || y === 0 || x === 15 || y === 15) col = [96, 134, 174];
            else if (y === 1 || x === 1) col = [246, 252, 255];
            else if (y === 2 && (x % 2 === 0 || row === 1)) col = [238, 198, 92];
            else if (y >= 12 || (y === 8 && x % 4 !== 0)) col = [176, 204, 232];
            setPx(ox + x, oy + y, col);
          }
        }
      }
      // 8: CloudPlatform
      {
        const ox = 8 * 16;
        for (let y = 0; y < 8; y++) {
          for (let x = 0; x < 16; x++) {
            const puff = Math.sin((x + row * 3) * 0.48);
            const topY = puff > 0.35 ? 0 : 1;
            const botY = puff < -0.2 ? 6 : 7;
            let col = null;
            if (y === topY && x >= 1 && x <= 14) col = [252, 254, 255];
            else if (y > topY && y < botY - 1) col = y <= 2 ? [252, 254, 255] : [226, 244, 255];
            else if (y === botY - 1 && x >= 1 && x <= 14) col = [174, 214, 246];
            else if (y === botY && x >= 2 && x <= 13) col = [118, 178, 224];
            if ((x === 0 || x === 15) && y >= 2 && y <= 4) col = [118, 178, 224];
            setPx(ox + x, oy + y, col);
          }
        }
      }
    }
    g.putImageData(img, 0, 0);
    return c;
  }

  function buildCoinAtlas() {
    const c = makeCanvas(4 * 12, 3 * 12);
    const g = c.getContext("2d");
    const img = g.createImageData(c.width, c.height);
    const px = img.data;
    const pals = [
      [[120,62,24], [214,130,56], [252,198,120], [255,228,178]],
      [[152,92,14], [246,194,42], [255,238,118], [255,252,200]],
      [[44,122,166], [110,226,252], [224,252,255], [255,240,120]],
    ];
    const scales = [1.0, 0.72, 0.34, 0.72];
    for (let tier = 0; tier < 3; tier++) {
      const pal = pals[tier];
      for (let f = 0; f < 4; f++) {
        const ox = f * 12, oy = tier * 12;
        const rx = 4.6 * scales[f], ry = tier === 2 ? 5.1 : 4.6;
        for (let y = 0; y < 12; y++) {
          for (let x = 0; x < 12; x++) {
            const dx = (x - 5.5) / Math.max(1.1, rx);
            const dy = (y - 5.5) / ry;
            const d = tier === 2 ? (Math.abs(dx) * 0.78 + Math.abs(dy) * 0.78) : Math.hypot(dx, dy);
            if (d <= 1.08) {
              let col = pal[1];
              if (d >= 0.80) col = pal[0];
              else if (dx + dy < -0.35) col = pal[2];
              else if (Math.abs(dx) < 0.25 && Math.abs(dy) < 0.45) col = pal[3];
              const i = ((oy + y) * c.width + (ox + x)) * 4;
              px[i] = col[0]; px[i+1] = col[1]; px[i+2] = col[2]; px[i+3] = 255;
            }
          }
        }
      }
    }
    g.putImageData(img, 0, 0);
    return c;
  }

  function buildPlayerAtlas() {
    const c = makeCanvas(8 * 16, 16);
    const g = c.getContext("2d");
    const img = g.createImageData(c.width, c.height);
    const px = img.data;
    const plot = (ox, x, y, col) => {
      if (x < 0 || x >= 16 || y < 0 || y >= 16) return;
      const i = (y * c.width + (ox + x)) * 4;
      px[i] = col[0]; px[i+1] = col[1]; px[i+2] = col[2]; px[i+3] = 255;
    };
    const outline = [18,16,28], cloak = [56,132,206], cloakHi = [102,186,246], skin = [246,208,174], eye = [28,24,42];

    for (let f = 0; f < 8; f++) {
      const ox = f * 16;
      const scarf = f === 7 ? [138,250,255] : [242,114,72];
      const scarfHi = f === 7 ? [230,255,255] : [255,184,112];
      const boot = f === 7 ? [110,244,255] : [76,54,44];
      let bobY = (f === 1 || f === 3 || f === 5 || f === 7) ? -1 : 0;
      const scarfWave = (f === 2 || f === 4 || f === 7) ? -1 : 0;

      for (let sx = 2; sx <= 5; sx++) {
        plot(ox, sx, 7 + bobY + (sx < 4 ? scarfWave : 0), sx === 2 ? scarfHi : scarf);
        plot(ox, sx, 8 + bobY + (sx < 4 ? scarfWave : 0), outline);
      }
      for (let y = 2 + bobY; y <= 7 + bobY; y++) {
        for (let x = 5; x <= 11; x++) {
          if ((y === 2 + bobY || y === 7 + bobY) && (x === 5 || x === 11)) continue;
          const edge = x === 5 || x === 11 || y === 2 + bobY || y === 7 + bobY;
          let col = edge ? outline : (y === 3 + bobY ? cloakHi : cloak);
          if (x >= 8 && x <= 10 && y >= 4 + bobY && y <= 6 + bobY) {
            col = (x === 10 && y === 5 + bobY) ? eye : skin;
          }
          plot(ox, x, y, col);
        }
      }
      for (let x = 5; x <= 10; x++) plot(ox, x, 7 + bobY, scarf);
      for (let y = 8 + bobY; y <= 11 + bobY; y++) {
        for (let x = 5; x <= 10; x++) {
          plot(ox, x, y, (x === 5 || x === 10) ? outline : (x <= 7 ? cloakHi : cloak));
        }
      }
      let lfx = 6, rfx = 9, lfy = 14, rfy = 14;
      if (f === 1) { lfx = 4; rfx = 10; lfy = 13; rfy = 14; }
      else if (f === 2) { lfx = 5; rfx = 9; }
      else if (f === 3) { lfx = 10; rfx = 5; lfy = 14; rfy = 13; }
      else if (f === 5) { lfx = 5; rfx = 10; lfy = 12; rfy = 13; }
      else if (f === 6) { lfx = 5; rfx = 10; lfy = 14; rfy = 13; }
      else if (f === 7) { lfx = 5; rfx = 10; lfy = 13; rfy = 13; }
      for (let fy = 12 + bobY; fy <= lfy; fy++) {
        plot(ox, lfx, fy, fy >= lfy - 1 ? boot : outline);
        plot(ox, lfx + 1, fy, fy >= lfy - 1 ? boot : cloak);
      }
      for (let fy = 12 + bobY; fy <= rfy; fy++) {
        plot(ox, rfx, fy, fy >= rfy - 1 ? boot : cloak);
        plot(ox, rfx + 1, fy, fy >= rfy - 1 ? boot : outline);
      }
    }
    g.putImageData(img, 0, 0);
    return c;
  }

  function buildGoalArch() {
    const c = makeCanvas(48, 56);
    const g = c.getContext("2d");
    const img = g.createImageData(48, 56);
    const px = img.data;
    for (let y = 0; y < 56; y++) {
      for (let x = 0; x < 48; x++) {
        const cx = x - 23.5, cy = y - 22;
        const inOpen = Math.abs(cx) <= 12 && y >= 14 && (y >= 22 || (cx * cx + cy * cy <= 144));
        const inRim = Math.abs(cx) <= 18 && y >= 6 && (y >= 22 || (cx * cx + cy * cy <= 342.25));
        let col = null;
        if (y >= 50 && x >= 2 && x <= 45) {
          col = (y === 50 || y === 55 || x === 2 || x === 45) ? [82,118,162,255] : (y === 51 ? [248,208,84,255] : [232,244,255,255]);
        } else if (inOpen) {
          const core = 1 - Math.abs(cx) / 12;
          col = core > 0.45 ? [238,254,255,220] : [102,224,255,190];
        } else if (inRim) {
          const outerEdge = y === 6 || x === 6 || x === 41 || (y < 22 && cx * cx + cy * cy >= 272.25);
          const innerGold = (y < 22 && cx * cx + cy * cy <= 210.25) || (Math.abs(cx) >= 12 && Math.abs(cx) <= 13.5);
          col = outerEdge ? [82,118,162,255] : (innerGold ? [248,208,84,255] : (cx < 0 ? [232,244,255,255] : [176,206,236,255]));
        }
        if (col) {
          const i = (y * 48 + x) * 4;
          px[i] = col[0]; px[i+1] = col[1]; px[i+2] = col[2]; px[i+3] = col[3];
        }
      }
    }
    g.putImageData(img, 0, 0);
    return c;
  }

  function makeGlowCanvas(r, g, b, a) {
    const c = makeCanvas(32, 32);
    const gc = c.getContext("2d");
    const grad = gc.createRadialGradient(16, 16, 0, 16, 16, 16);
    grad.addColorStop(0, `rgba(${r},${g},${b},${a})`);
    grad.addColorStop(1, `rgba(${r},${g},${b},0)`);
    gc.fillStyle = grad;
    gc.fillRect(0, 0, 32, 32);
    return c;
  }

  const tileAtlas = buildTileAtlas();
  const coinAtlas = buildCoinAtlas();
  const playerAtlas = buildPlayerAtlas();
  const goalArch = buildGoalArch();
  const glowCyan = makeGlowCanvas(110, 240, 255, 0.55);
  const glowGold = makeGlowCanvas(255, 224, 96, 0.45);
  const glowAmber = makeGlowCanvas(255, 198, 92, 0.42);

  // =========================================================================
  // WEBAUDIO PROCEDURAL SYNTHESIZER (matches AudioSynthesizer.cs)
  // =========================================================================
  class AudioSynth {
    constructor() {
      this.ctx = null;
      this.muted = false;
    }
    unlock() {
      if (!this.ctx) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (AC) this.ctx = new AC();
      }
      if (this.ctx && this.ctx.state === "suspended") {
        this.ctx.resume().catch(() => {});
      }
    }
    tone(freq, endFreq, dur, vol = 0.14, type = "sine", delay = 0) {
      if (this.muted || !this.ctx || this.ctx.state !== "running") return;
      const t0 = this.ctx.currentTime + delay;
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = type;
      osc.frequency.setValueAtTime(freq, t0);
      if (endFreq !== freq) osc.frequency.exponentialRampToValueAtTime(Math.max(40, endFreq), t0 + dur);
      gain.gain.setValueAtTime(0.001, t0);
      gain.gain.linearRampToValueAtTime(vol, t0 + 0.008);
      gain.gain.exponentialRampToValueAtTime(0.001, t0 + dur);
      osc.connect(gain);
      gain.connect(this.ctx.destination);
      osc.start(t0);
      osc.stop(t0 + dur + 0.02);
    }
    arp(notes, stepDur, vol) {
      notes.forEach((f, idx) => this.tone(f, f, stepDur + 0.05, vol, "sine", idx * stepDur));
    }
    playJump(hLv) {
      const mul = 1 + Math.min(0.35, hLv * 0.06);
      this.tone(240 * mul, 520 * mul, 0.11, 0.12, "triangle");
    }
    playLand() { this.tone(110, 58, 0.065, 0.08, "triangle"); }
    playCoin(v) {
      if (v >= 5) this.arp([587, 880, 1175, 1760], 0.055, 0.12);
      else if (v >= 3) this.arp([659, 880, 1318], 0.05, 0.11);
      else this.arp([784, 1175], 0.055, 0.10);
    }
    playMenuMove() { this.tone(540, 540, 0.035, 0.07, "sine"); }
    playMenuError() { this.arp([190, 145], 0.06, 0.10); }
    playShopOpen() { this.arp([440, 554, 659], 0.045, 0.10); }
    playShopClose() { this.arp([659, 554, 440], 0.04, 0.09); }
    playUpgrade(lv) {
      const s = 1 + Math.max(0, lv - 1) * 0.04;
      this.arp([523.25*s, 659.25*s, 783.99*s, 1046.5*s], 0.06, 0.12);
    }
    playVictory() { this.arp([523.25, 659.25, 783.99, 987.77, 1174.66, 1567.98], 0.09, 0.14); }
  }
  const audio = new AudioSynth();

  // =========================================================================
  // SAVE MANAGER (localStorage)
  // =========================================================================
  function loadSave() {
    try {
      const raw = localStorage.getItem(SAVE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        return {
          bestTimeSeconds: typeof parsed.bestTimeSeconds === "number" ? parsed.bestTimeSeconds : null,
          bestCoinsCollected: parsed.bestCoinsCollected || 0,
          runsCompleted: parsed.runsCompleted || 0,
        };
      }
    } catch {}
    return { bestTimeSeconds: null, bestCoinsCollected: 0, runsCompleted: 0 };
  }

  function recordRun(save, runSeconds, coinsCollected) {
    const isNew = save.bestTimeSeconds === null || runSeconds < save.bestTimeSeconds;
    if (isNew) save.bestTimeSeconds = runSeconds;
    if (coinsCollected > save.bestCoinsCollected) save.bestCoinsCollected = coinsCollected;
    save.runsCompleted++;
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(save)); } catch {}
    return isNew;
  }

  function formatTime(seconds) {
    if (seconds < 0) seconds = 0;
    const mins = Math.floor(seconds / 60);
    const rem = seconds - mins * 60;
    return `${String(mins).padStart(2, "0")}:${rem.toFixed(2).padStart(5, "0")}`;
  }

  // =========================================================================
  // PLAYER STATS & PHYSICS SIMULATION (matches StatsAndUpgrades.cs)
  // =========================================================================
  const S = DATA.stats;
  const STAT_ORDER = [0, 1, 2]; // 0=Speed, 1=JumpHeight, 2=JumpDuration
  const STAT_META = [
    { name: "MOVE SPEED", desc: "Run faster & leap farther across wide gaps", accent: "#ffba4a", fmt: (l) => `${S.runSpeeds[l].toFixed(0)} px/s` },
    { name: "JUMP HEIGHT", desc: "Stronger upward spring to reach high ledges", accent: "#6ce88a", fmt: (l) => `${S.jumpVelocities[l].toFixed(0)} impulse` },
    { name: "JUMP DURATION", desc: "Hold Jump longer for extended skyward lift", accent: "#62e0ff", fmt: (l) => `${S.jumpSustainTimes[l].toFixed(2)}s lift` },
  ];

  function simulateMaxJump(sLv, hLv, dLv) {
    const dt = 1 / 60;
    let vx = S.runSpeeds[sLv];
    let vy = -S.jumpVelocities[hLv];
    let sustainLeft = S.jumpSustainTimes[dLv];
    let x = 0, y = 0, minY = 0;
    for (let i = 0; i < 300; i++) {
      let gScale = 1;
      if (vy < 0 && sustainLeft > 0) { gScale = S.sustainGravityScale; sustainLeft -= dt; }
      else if (Math.abs(vy) < S.apexThresholdVy) gScale = S.apexGravityScale;
      else if (vy > 0) gScale = S.fallGravityScale;
      vy = Math.min(S.maxFallSpeed, vy + S.baseGravity * gScale * dt);
      x += vx * dt;
      y += vy * dt;
      if (y < minY) minY = y;
      if (y >= 0 && vy > 0) break;
    }
    return { maxHeightPx: -minY, maxDistancePx: x };
  }

  const stats = {
    levels: [0, 0, 0],
    coins: 0,
    totalCoinsCollected: 0,
    upgradesPurchased: 0,
    reset() {
      this.levels = [0, 0, 0];
      this.coins = 0;
      this.totalCoinsCollected = 0;
      this.upgradesPurchased = 0;
    },
    isMaxed(t) { return this.levels[t] >= S.maxLevel; },
    getCost(t) { return this.isMaxed(t) ? 0 : S.upgradeCosts[this.levels[t]]; },
    canAfford(t) { return !this.isMaxed(t) && this.coins >= this.getCost(t); },
    tryUpgrade(t) {
      if (!this.canAfford(t)) return false;
      this.coins -= this.getCost(t);
      this.levels[t]++;
      this.upgradesPurchased++;
      return true;
    },
  };

  // =========================================================================
  // INPUT (Keyboard + HTML5 Gamepad + Mouse/Touch; Up NEVER Jumps)
  // =========================================================================
  const keysDown = new Set();
  const touchState = { enabled: false, left: false, right: false, jump: false, drop: false };
  const mouse = { x: -1, y: -1, moved: false, clicked: false };

  window.addEventListener("keydown", (e) => {
    audio.unlock();
    if (["Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Tab", "F1"].includes(e.code)) {
      e.preventDefault();
    }
    keysDown.add(e.code);
  });
  window.addEventListener("keyup", (e) => {
    keysDown.delete(e.code);
  });
  window.addEventListener("blur", () => keysDown.clear());

  function toUiCoords(clientX, clientY) {
    const r = canvas.getBoundingClientRect();
    return {
      x: Math.round(((clientX - r.left) / Math.max(1, r.width)) * UI_W),
      y: Math.round(((clientY - r.top) / Math.max(1, r.height)) * UI_H),
    };
  }

  canvas.addEventListener("mousemove", (e) => {
    const p = toUiCoords(e.clientX, e.clientY);
    if (p.x !== mouse.x || p.y !== mouse.y) mouse.moved = true;
    mouse.x = p.x; mouse.y = p.y;
  });
  canvas.addEventListener("mousedown", (e) => {
    audio.unlock();
    canvas.focus();
    if (e.button === 0) {
      const p = toUiCoords(e.clientX, e.clientY);
      mouse.x = p.x; mouse.y = p.y;
      mouse.clicked = true;
    }
  });

  // Touch handling for mobile / Steam Deck touchscreen
  const activeTouches = new Map();
  function syncTouches() {
    touchState.left = false;
    touchState.right = false;
    touchState.jump = false;
    touchState.drop = false;
    if (screen !== "Playing") return;
    for (const p of activeTouches.values()) {
      if (p.y > UI_H - 185) {
        if (p.x < 170) touchState.left = true;
        else if (p.x < 340) touchState.right = true;
        else if (p.x > UI_W - 220) touchState.jump = true;
        else if (p.x > UI_W - 400) touchState.drop = true;
      }
    }
  }
  canvas.addEventListener("touchstart", (e) => {
    e.preventDefault();
    audio.unlock();
    touchState.enabled = true;
    for (const t of e.changedTouches) {
      const p = toUiCoords(t.clientX, t.clientY);
      activeTouches.set(t.identifier, p);
      mouse.x = p.x; mouse.y = p.y;
      mouse.clicked = true;
    }
    syncTouches();
  }, { passive: false });
  canvas.addEventListener("touchmove", (e) => {
    e.preventDefault();
    for (const t of e.changedTouches) {
      activeTouches.set(t.identifier, toUiCoords(t.clientX, t.clientY));
    }
    syncTouches();
  }, { passive: false });
  canvas.addEventListener("touchend", (e) => {
    e.preventDefault();
    for (const t of e.changedTouches) activeTouches.delete(t.identifier);
    syncTouches();
  }, { passive: false });
  canvas.addEventListener("touchcancel", (e) => {
    for (const t of e.changedTouches) activeTouches.delete(t.identifier);
    syncTouches();
  });

  let prevSnap = {
    jump: false, shop: false, help: false, pause: false, confirm: false, back: false, restart: false,
    left: false, right: false, navY: 0, navTimer: 0, mKey: false, f11: false,
  };
  const input = {
    moveX: 0, moveY: 0,
    jumpPressed: false, jumpHeld: false, jumpReleased: false,
    shopToggle: false, helpToggle: false, pausePressed: false,
    confirmPressed: false, backPressed: false, restartPressed: false,
    mPressed: false, escPressed: false, fullscreenToggle: false,
    menuMoveY: 0, menuMoveX: 0,
    mouseX: -1, mouseY: -1, mouseMoved: false, mouseClicked: false,
  };

  function getBestGamepad() {
    if (!navigator.getGamepads) return null;
    const list = navigator.getGamepads();
    for (const gp of list) if (gp && gp.connected) return gp;
    return null;
  }

  function pollInput(dt) {
    const gp = getBestGamepad();
    const btn = (idx) => !!(gp && gp.buttons[idx] && gp.buttons[idx].pressed);

    let kx = (keysDown.has("ArrowRight") || keysDown.has("KeyD") ? 1 : 0) - (keysDown.has("ArrowLeft") || keysDown.has("KeyA") ? 1 : 0);
    if (touchState.left) kx = -1;
    if (touchState.right) kx = 1;
    let gx = gp && Math.abs(gp.axes[0] || 0) >= 0.25 ? gp.axes[0] : 0;
    if (btn(14)) gx = -1;
    if (btn(15)) gx = 1;
    input.moveX = Math.max(-1, Math.min(1, Math.abs(gx) >= Math.abs(kx) ? gx : kx));

    let ky = (keysDown.has("ArrowDown") || keysDown.has("KeyS") ? 1 : 0) - (keysDown.has("ArrowUp") || keysDown.has("KeyW") ? 1 : 0);
    if (touchState.drop) ky = 1;
    let gy = gp && Math.abs(gp.axes[1] || 0) >= 0.35 ? gp.axes[1] : 0;
    if (btn(12)) gy = -1;
    if (btn(13)) gy = 1;
    input.moveY = Math.max(-1, Math.min(1, Math.abs(gy) >= Math.abs(ky) ? gy : ky));

    // Jump: A button (0), Space, Z, K (Up / W NEVER jumps!)
    const currJump = btn(0) || keysDown.has("Space") || keysDown.has("KeyZ") || keysDown.has("KeyK") || touchState.jump || (touchState.drop && false);
    input.jumpHeld = currJump;
    input.jumpPressed = currJump && !prevSnap.jump;
    input.jumpReleased = !currJump && prevSnap.jump;

    // Touch Drop button triggers Down + Jump tap
    if (touchState.drop && !prevSnap.dropTouch) {
      input.moveY = 1;
      input.jumpPressed = true;
    }
    prevSnap.dropTouch = touchState.drop;

    const currShop = btn(3) || keysDown.has("Tab") || keysDown.has("KeyE") || keysDown.has("KeyI");
    const currHelp = btn(2) || keysDown.has("KeyH") || keysDown.has("F1");
    const currPause = btn(9) || keysDown.has("Escape") || keysDown.has("KeyP");
    const currConfirm = btn(0) || keysDown.has("Enter") || keysDown.has("Space");
    const currBack = btn(1) || keysDown.has("Escape") || keysDown.has("Backspace") || keysDown.has("KeyB");
    const currRestart = btn(8) || keysDown.has("KeyR");
    const currM = keysDown.has("KeyM");
    const currEsc = keysDown.has("Escape");
    const currF11 = keysDown.has("F11");

    input.shopToggle = currShop && !prevSnap.shop;
    input.helpToggle = currHelp && !prevSnap.help;
    input.pausePressed = currPause && !prevSnap.pause;
    input.confirmPressed = currConfirm && !prevSnap.confirm;
    input.backPressed = currBack && !prevSnap.back;
    input.restartPressed = currRestart && !prevSnap.restart;
    input.mPressed = currM && !prevSnap.mKey;
    input.escPressed = currEsc && !prevSnap.esc;
    input.fullscreenToggle = currF11 && !prevSnap.f11;

    const rawY = input.moveY < -0.5 ? -1 : (input.moveY > 0.5 ? 1 : 0);
    if (rawY === 0) {
      input.menuMoveY = 0; prevSnap.navY = 0; prevSnap.navTimer = 0;
    } else if (rawY !== prevSnap.navY) {
      input.menuMoveY = rawY; prevSnap.navY = rawY; prevSnap.navTimer = 0.30;
    } else {
      prevSnap.navTimer -= dt;
      if (prevSnap.navTimer <= 0) { input.menuMoveY = rawY; prevSnap.navTimer = 0.11; }
      else input.menuMoveY = 0;
    }

    const lNow = input.moveX < -0.55;
    const rNow = input.moveX > 0.55;
    input.menuMoveX = (rNow && !prevSnap.right ? 1 : 0) - (lNow && !prevSnap.left ? 1 : 0);

    input.mouseX = mouse.x;
    input.mouseY = mouse.y;
    input.mouseMoved = mouse.moved;
    input.mouseClicked = mouse.clicked;
    mouse.moved = false;
    mouse.clicked = false;

    prevSnap.jump = currJump;
    prevSnap.shop = currShop;
    prevSnap.help = currHelp;
    prevSnap.pause = currPause;
    prevSnap.confirm = currConfirm;
    prevSnap.back = currBack;
    prevSnap.restart = currRestart;
    prevSnap.mKey = currM;
    prevSnap.esc = currEsc;
    prevSnap.f11 = currF11;
    prevSnap.left = lNow;
    prevSnap.right = rNow;
  }

  // =========================================================================
  // WORLD ENTITIES: PLAYER, COINS, PARTICLES, BACKGROUND
  // =========================================================================
  const coins = DATA.coins.map((c) => ({ ...c, collected: false }));
  const totalPossibleCoins = coins.reduce((a, b) => a + b.v, 0);
  const particles = [];
  const popups = [];

  const motes = [];
  let rng = 20261121;
  const nextRand = () => { rng = (rng * 1664525 + 1013904223) >>> 0; return rng / 4294967296; };
  for (let i = 0; i < 90; i++) {
    motes.push({
      x: nextRand() * 640,
      y: nextRand() * 400,
      speed: 8 + nextRand() * 20,
      phase: nextRand() * Math.PI * 2,
      size: nextRand() > 0.72 ? 2 : 1,
    });
  }

  const player = {
    x: DATA.playerSpawn.x, y: DATA.playerSpawn.y,
    vx: 0, vy: 0,
    onGround: false, facingRight: true, sustaining: false,
    coyote: 0, jumpBuffer: 0, sustainLeft: 0, dropThrough: 0,
    animTimer: 0, dustTimer: 0, sx: 1, sy: 1,
    spawn() {
      this.x = DATA.playerSpawn.x; this.y = DATA.playerSpawn.y;
      this.vx = 0; this.vy = 0; this.onGround = false; this.facingRight = true;
      this.sustaining = false; this.coyote = 0; this.jumpBuffer = 0;
      this.sustainLeft = 0; this.dropThrough = 0; this.animTimer = 0;
      this.dustTimer = 0; this.sx = 1; this.sy = 1;
    },
    hitbox() {
      return { x: Math.floor(this.x - 5), y: Math.floor(this.y - 14), w: 10, h: 14 };
    },
    standingOnlyOnOneWay() {
      const ft = Math.floor((this.y + 1) / TILE_SIZE);
      const lt = Math.floor((this.x - 4.5) / TILE_SIZE);
      const rt = Math.floor((this.x + 4.5) / TILE_SIZE);
      let saw = false;
      for (let tx = lt; tx <= rt; tx++) {
        const t = getTile(tx, ft);
        if (isSolid(t)) return false;
        if (isOneWay(t)) saw = true;
      }
      return saw;
    },
    update(dt) {
      const wasGround = this.onGround;
      const prevFeet = this.y;
      if (this.dropThrough > 0) this.dropThrough = Math.max(0, this.dropThrough - dt);
      this.sx += (1 - this.sx) * Math.min(1, dt * 14);
      this.sy += (1 - this.sy) * Math.min(1, dt * 14);

      this.coyote = this.onGround ? 0.10 : Math.max(0, this.coyote - dt);

      if (input.jumpPressed) {
        if (this.onGround && input.moveY > 0.65 && this.standingOnlyOnOneWay()) {
          this.dropThrough = 0.18;
          this.onGround = false;
          this.coyote = 0;
          this.jumpBuffer = 0;
          this.y += 2;
          this.vy = 35;
        } else {
          this.jumpBuffer = 0.12;
        }
      } else {
        this.jumpBuffer = Math.max(0, this.jumpBuffer - dt);
      }

      const spdLv = stats.levels[0];
      const hgtLv = stats.levels[1];
      const durLv = stats.levels[2];
      const targetVx = input.moveX * S.runSpeeds[spdLv];
      const accel = this.onGround ? S.groundAccels[spdLv] : S.airAccels[spdLv];
      if (Math.abs(input.moveX) > 0.05) this.facingRight = input.moveX > 0;
      if (this.vx < targetVx) this.vx = Math.min(targetVx, this.vx + accel * dt);
      else if (this.vx > targetVx) this.vx = Math.max(targetVx, this.vx - accel * dt);

      if (this.jumpBuffer > 0 && this.coyote > 0) {
        this.vy = -S.jumpVelocities[hgtLv];
        this.sustainLeft = S.jumpSustainTimes[durLv];
        this.coyote = 0;
        this.jumpBuffer = 0;
        this.onGround = false;
        this.sx = 0.76; this.sy = 1.28;
        audio.playJump(hgtLv);
        spawnDust(this.x, this.y, 6, "#d2e8ff");
      }

      if (input.jumpReleased && this.vy < -55) {
        this.vy *= S.earlyReleaseCut;
        this.sustainLeft = 0;
      }

      this.sustaining = false;
      let gScale = 1;
      if (this.vy < 0 && input.jumpHeld && this.sustainLeft > 0) {
        gScale = S.sustainGravityScale;
        this.sustainLeft -= dt;
        this.sustaining = true;
        if (particles.length < 220) {
          particles.push({
            x: this.x + (Math.random() - 0.5) * 8, y: this.y - 2,
            vx: (Math.random() - 0.5) * 26, vy: 35 + Math.random() * 35,
            color: "#80f4ff", life: 0.20, maxLife: 0.20, size: 2,
          });
        }
      } else if (Math.abs(this.vy) < S.apexThresholdVy) gScale = S.apexGravityScale;
      else if (this.vy > 0) gScale = S.fallGravityScale;

      this.vy = Math.min(S.maxFallSpeed, this.vy + S.baseGravity * gScale * dt);

      // Move X
      const dx = this.vx * dt;
      this.x = Math.max(5, Math.min(WORLD_W - 5, this.x + dx));
      const tt = Math.floor((this.y - 13) / TILE_SIZE);
      const bt = Math.floor((this.y - 1) / TILE_SIZE);
      const lt = Math.floor((this.x - 5) / TILE_SIZE);
      const rt = Math.floor((this.x + 5 - 0.01) / TILE_SIZE);
      for (let ty = tt; ty <= bt; ty++) {
        for (let tx = lt; tx <= rt; tx++) {
          if (!isSolid(getTile(tx, ty))) continue;
          if (dx > 0) { this.x = tx * TILE_SIZE - 5; this.vx = 0; }
          else if (dx < 0) { this.x = (tx + 1) * TILE_SIZE + 5; this.vx = 0; }
        }
      }

      // Move Y
      const dy = this.vy * dt;
      this.onGround = false;
      this.y += dy;
      if (this.y > WORLD_H) {
        this.y = WORLD_H; this.vy = 0; this.onGround = true;
      } else {
        const ylt = Math.floor((this.x - 4.5) / TILE_SIZE);
        const yrt = Math.floor((this.x + 4.5) / TILE_SIZE);
        if (dy >= 0) {
          const ft = Math.floor((this.y - 0.01) / TILE_SIZE);
          const st = Math.max(0, Math.floor((prevFeet - 0.01) / TILE_SIZE));
          outer: for (let ty = st; ty <= ft; ty++) {
            const topPx = ty * TILE_SIZE;
            for (let tx = ylt; tx <= yrt; tx++) {
              const t = getTile(tx, ty);
              if (isSolid(t) || (isOneWay(t) && this.dropThrough <= 0 && prevFeet <= topPx + 2.5)) {
                this.y = topPx; this.vy = 0; this.onGround = true;
                break outer;
              }
            }
          }
        } else {
          const ht = Math.floor((this.y - 14) / TILE_SIZE);
          for (let tx = ylt; tx <= yrt; tx++) {
            if (isSolid(getTile(tx, ht))) {
              this.y = (ht + 1) * TILE_SIZE + 14; this.vy = 0; break;
            }
          }
        }
      }

      if (!wasGround && this.onGround) {
        this.sx = 1.26; this.sy = 0.78;
        audio.playLand();
        spawnDust(this.x, this.y, 5, "#c3cee0");
      }
      if (this.onGround && Math.abs(this.vx) > 45) {
        this.animTimer += dt * (6 + Math.abs(this.vx) * 0.045);
        this.dustTimer -= dt;
        if (this.dustTimer <= 0) {
          this.dustTimer = Math.max(0.08, 0.22 - spdLv * 0.025);
          spawnDust(this.x, this.y, 2, "#b0bace");
        }
      } else {
        this.animTimer += dt * 3;
      }
    },
  };

  function spawnDust(x, y, count, color) {
    for (let i = 0; i < count && particles.length < 240; i++) {
      particles.push({
        x: x + (Math.random() - 0.5) * 8, y: y - 1,
        vx: (Math.random() - 0.5) * 48, vy: -10 - Math.random() * 26,
        color, life: 0.24, maxLife: 0.24, size: Math.random() > 0.5 ? 2 : 1,
      });
    }
  }

  // =========================================================================
  // GAME STATE & SCREEN LOGIC
  // =========================================================================
  let screen = "MainMenu";
  let howToPlayReturnTo = "MainMenu";
  let save = loadSave();
  let camX = 0, camY = 0;
  let elapsedVisual = 0;
  let runTimer = 0;
  let physicsAccum = 0;
  let shakeTimer = 0;
  let newRecord = false;
  let mainMenuIdx = 0;
  let pauseIdx = 0;
  let victoryIdx = 0;
  let shopIdx = 0;
  let shopFlashTimer = 0;
  let shopFlashErr = false;

  function computeTargetCam(px, py) {
    return {
      x: Math.max(0, Math.min(WORLD_W - VIRTUAL_W, px - VIRTUAL_W * 0.5)),
      y: Math.max(0, Math.min(WORLD_H - VIRTUAL_H, py - VIRTUAL_H * 0.56)),
    };
  }

  function resetRun(startPlaying) {
    stats.reset();
    for (const c of coins) c.collected = false;
    player.spawn();
    particles.length = 0;
    popups.length = 0;
    runTimer = 0;
    physicsAccum = 0;
    newRecord = false;
    pauseIdx = 0;
    victoryIdx = 0;
    const c = computeTargetCam(player.x, player.y);
    camX = c.x; camY = c.y;
    screen = startPlaying ? "Playing" : "MainMenu";
    syncTouches();
  }
  resetRun(false);

  function toggleFullscreen() {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen?.().catch(() => {});
    } else {
      document.exitFullscreen?.().catch(() => {});
    }
  }

  function rectContains(r, x, y) {
    return x >= r.x && x < r.x + r.w && y >= r.y && y < r.y + r.h;
  }
  function getMainMenuItemRect(i) {
    const bx = (UI_W - 720) / 2, by = (UI_H - 520) / 2;
    return { x: bx + 110, y: by + 186 + i * 58, w: 500, h: 46 };
  }
  function getPauseItemRect(i) {
    const bx = (UI_W - 480) / 2, by = (UI_H - 430) / 2;
    return { x: bx + 48, y: by + 94 + i * 46 - 5, w: 384, h: 36 };
  }
  function getVictoryBtnRect(i) {
    const bx = (UI_W - 680) / 2, by = (UI_H - 500) / 2;
    return { x: bx + 78 + i * 284, y: by + 382, w: 240, h: 56 };
  }

  function updateParticlesAndPopups(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.life -= dt;
      if (p.life <= 0) particles.splice(i, 1);
      else { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 90 * dt; }
    }
    for (let i = popups.length - 1; i >= 0; i--) {
      const pop = popups[i];
      pop.life -= dt;
      if (pop.life <= 0) popups.splice(i, 1);
      else pop.y -= 20 * dt;
    }
  }

  function update(dt) {
    elapsedVisual += dt;
    pollInput(dt);
    canvas.style.cursor = screen === "Playing" ? "none" : "default";
    if (input.fullscreenToggle) toggleFullscreen();
    if (shakeTimer > 0) shakeTimer = Math.max(0, shakeTimer - dt);

    if (screen === "MainMenu") {
      const tc = computeTargetCam(player.x + Math.sin(elapsedVisual * 0.4) * 40, player.y);
      camX += (tc.x - camX) * Math.min(1, dt * 3);
      camY += (tc.y - camY) * Math.min(1, dt * 3);

      const count = 4;
      if (input.menuMoveY !== 0) {
        mainMenuIdx = (mainMenuIdx + input.menuMoveY + count) % count;
        audio.playMenuMove();
      }
      if (input.helpToggle) {
        howToPlayReturnTo = "MainMenu";
        audio.playShopOpen();
        screen = "HowToPlay";
        return;
      }
      let clicked = false;
      for (let i = 0; i < count; i++) {
        if (rectContains(getMainMenuItemRect(i), input.mouseX, input.mouseY)) {
          if (input.mouseMoved && mainMenuIdx !== i) { mainMenuIdx = i; audio.playMenuMove(); }
          if (input.mouseClicked) { mainMenuIdx = i; clicked = true; }
        }
      }
      if (input.confirmPressed || clicked) {
        if (mainMenuIdx === 0) { audio.playShopOpen(); resetRun(true); }
        else if (mainMenuIdx === 1) { howToPlayReturnTo = "MainMenu"; audio.playShopOpen(); screen = "HowToPlay"; }
        else if (mainMenuIdx === 2) { toggleFullscreen(); audio.playMenuMove(); }
        else if (mainMenuIdx === 3) { audio.muted = !audio.muted; audio.playMenuMove(); }
      }
    } else if (screen === "Playing") {
      // Allow clickable HUD badges (Shop / How To Play / Pause)
      if (input.mouseClicked && !touchState.enabled) {
        if (rectContains({ x: UI_W - 332, y: 18, w: 312, h: 82 }, input.mouseX, input.mouseY)) {
          shopIdx = 0; shopFlashTimer = 0; audio.playShopOpen(); screen = "Shop"; return;
        }
        if (rectContains({ x: 444, y: UI_H - 54, w: 198, h: 36 }, input.mouseX, input.mouseY)) {
          howToPlayReturnTo = "Playing"; audio.playShopOpen(); screen = "HowToPlay"; return;
        }
      }
      if (touchState.enabled && input.mouseClicked) {
        if (rectContains({ x: UI_W - 332, y: 18, w: 312, h: 82 }, input.mouseX, input.mouseY)) {
          shopIdx = 0; shopFlashTimer = 0; audio.playShopOpen(); screen = "Shop"; return;
        }
        if (rectContains({ x: 444, y: UI_H - 54, w: 198, h: 36 }, input.mouseX, input.mouseY)) {
          howToPlayReturnTo = "Playing"; audio.playShopOpen(); screen = "HowToPlay"; return;
        }
        if (rectContains({ x: 20, y: 18, w: 386, h: 96 }, input.mouseX, input.mouseY)) {
          pauseIdx = 0; audio.playMenuMove(); screen = "Paused"; return;
        }
      }

      if (input.pausePressed) { pauseIdx = 0; audio.playMenuMove(); screen = "Paused"; return; }
      if (input.shopToggle) { shopIdx = 0; shopFlashTimer = 0; audio.playShopOpen(); screen = "Shop"; return; }
      if (input.helpToggle) { howToPlayReturnTo = "Playing"; audio.playShopOpen(); screen = "HowToPlay"; return; }
      if (input.restartPressed) { audio.playMenuMove(); resetRun(true); return; }

      runTimer += dt;
      physicsAccum += dt;
      let steps = 0;
      while (physicsAccum >= FIXED_DT && steps < 5) {
        physicsAccum -= FIXED_DT;
        steps++;
        player.update(FIXED_DT);

        // Coin pickup
        const pb = player.hitbox();
        for (const c of coins) {
          if (c.collected) continue;
          if (pb.x - 3 < c.x + 6 && pb.x + pb.w + 3 > c.x - 6 && pb.y - 3 < c.y + 6 && pb.y + pb.h + 3 > c.y - 6) {
            c.collected = true;
            stats.coins += c.v;
            stats.totalCoinsCollected += c.v;
            audio.playCoin(c.v);
            const burst = c.v >= 5 ? "#76f0ff" : (c.v >= 3 ? "#ffe054" : "#fab25c");
            for (let i = 0; i < 10 && particles.length < 240; i++) {
              const a = i * (Math.PI * 2 / 10) + Math.random() * 0.4;
              const sp = 35 + Math.random() * 55;
              particles.push({ x: c.x, y: c.y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 15, color: burst, life: 0.36, maxLife: 0.36, size: 2 });
            }
            popups.push({ x: c.x, y: c.y - 8, text: `+${c.v}`, color: burst, life: 0.70, maxLife: 0.70 });
          }
        }

        // Goal trigger
        const gt = DATA.goalTrigger;
        if (pb.x < gt.x + gt.w && pb.x + pb.w > gt.x && pb.y < gt.y + gt.h && pb.y + pb.h > gt.y) {
          audio.playVictory();
          newRecord = recordRun(save, runTimer, stats.totalCoinsCollected);
          shakeTimer = 0.28;
          victoryIdx = 0;
          screen = "Victory";
          for (let i = 0; i < 48; i++) {
            const a = Math.random() * Math.PI * 2;
            const sp = 40 + Math.random() * 140;
            particles.push({
              x: DATA.goalArchBase.x, y: DATA.goalArchBase.y - 24,
              vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 35,
              color: i % 2 === 0 ? "#ffe86c" : "#80f2ff", life: 1.2, maxLife: 1.2, size: 2,
            });
          }
          return;
        }
      }

      updateParticlesAndPopups(dt);
      const tc = computeTargetCam(player.x, player.y);
      camX += (tc.x - camX) * Math.min(1, dt * 9.5);
      camY += (tc.y - camY) * Math.min(1, dt * 9.5);
    } else if (screen === "Shop") {
      // Speedrun timer is PAUSED in Shop
      if (shopFlashTimer > 0) shopFlashTimer = Math.max(0, shopFlashTimer - dt);
      if (input.shopToggle || input.backPressed) { audio.playShopClose(); screen = "Playing"; return; }
      if (input.menuMoveY !== 0) {
        shopIdx = (shopIdx + input.menuMoveY + 3) % 3;
        audio.playMenuMove();
      }
      const px = (UI_W - 900) / 2, py = (UI_H - 620) / 2;
      let clickedCard = false;
      for (let i = 0; i < 3; i++) {
        const cr = { x: px + 34, y: py + 140 + i * 138, w: 832, h: 122 };
        if (rectContains(cr, input.mouseX, input.mouseY)) {
          if (input.mouseMoved && shopIdx !== i) { shopIdx = i; audio.playMenuMove(); }
          if (input.mouseClicked) { shopIdx = i; clickedCard = true; }
        }
      }
      if (input.mouseClicked && !clickedCard) {
        const panelR = { x: px, y: py, w: 900, h: 620 };
        const closeR = { x: px + 460, y: py + 620 - 64, w: 410, h: 48 };
        if (!rectContains(panelR, input.mouseX, input.mouseY) || rectContains(closeR, input.mouseX, input.mouseY)) {
          audio.playShopClose(); screen = "Playing"; return;
        }
      }
      if (input.confirmPressed || clickedCard) {
        const st = STAT_ORDER[shopIdx];
        if (stats.tryUpgrade(st)) {
          audio.playUpgrade(stats.levels[st]);
          shopFlashTimer = 0.25; shopFlashErr = false;
        } else {
          audio.playMenuError();
          shopFlashTimer = 0.25; shopFlashErr = true;
        }
      }
    } else if (screen === "HowToPlay") {
      // Speedrun timer is PAUSED in How To Play
      if (input.helpToggle || input.backPressed || input.confirmPressed || input.mouseClicked) {
        audio.playShopClose();
        screen = howToPlayReturnTo;
      }
    } else if (screen === "Paused") {
      if (input.pausePressed || input.backPressed) { audio.playShopClose(); screen = "Playing"; return; }
      const count = 6;
      if (input.menuMoveY !== 0) { pauseIdx = (pauseIdx + input.menuMoveY + count) % count; audio.playMenuMove(); }
      let clicked = false;
      for (let i = 0; i < count; i++) {
        if (rectContains(getPauseItemRect(i), input.mouseX, input.mouseY)) {
          if (input.mouseMoved && pauseIdx !== i) { pauseIdx = i; audio.playMenuMove(); }
          if (input.mouseClicked) { pauseIdx = i; clicked = true; }
        }
      }
      if (input.confirmPressed || clicked) {
        if (pauseIdx === 0) { audio.playShopClose(); screen = "Playing"; }
        else if (pauseIdx === 1) { howToPlayReturnTo = "Paused"; audio.playShopOpen(); screen = "HowToPlay"; }
        else if (pauseIdx === 2) { audio.playShopOpen(); resetRun(true); }
        else if (pauseIdx === 3) { toggleFullscreen(); audio.playMenuMove(); }
        else if (pauseIdx === 4) { audio.muted = !audio.muted; audio.playMenuMove(); }
        else if (pauseIdx === 5) { audio.playMenuMove(); resetRun(false); }
      }
    } else if (screen === "Victory") {
      updateParticlesAndPopups(dt);
      if (input.menuMoveX < 0 || input.menuMoveY < 0) {
        if (victoryIdx !== 0) { victoryIdx = 0; audio.playMenuMove(); }
      } else if (input.menuMoveX > 0 || input.menuMoveY > 0) {
        if (victoryIdx !== 1) { victoryIdx = 1; audio.playMenuMove(); }
      }
      let clickedBtn = false;
      for (let i = 0; i < 2; i++) {
        if (rectContains(getVictoryBtnRect(i), input.mouseX, input.mouseY)) {
          if (input.mouseMoved && victoryIdx !== i) { victoryIdx = i; audio.playMenuMove(); }
          if (input.mouseClicked) { victoryIdx = i; clickedBtn = true; }
        }
      }
      if (input.backPressed || input.pausePressed || input.mPressed || ((input.confirmPressed || clickedBtn) && victoryIdx === 1)) {
        audio.playMenuMove();
        resetRun(false);
      } else if (input.restartPressed || ((input.confirmPressed || clickedBtn) && victoryIdx === 0)) {
        audio.playShopOpen();
        resetRun(true);
      }
    }
  }

  // =========================================================================
  // RENDERING
  // =========================================================================
  function lerpRGB(a, b, t) {
    t = Math.max(0, Math.min(1, t));
    return [
      Math.round(a[0] + (b[0] - a[0]) * t),
      Math.round(a[1] + (b[1] - a[1]) * t),
      Math.round(a[2] + (b[2] - a[2]) * t),
    ];
  }
  function rgbStr(c, alpha = 1) { return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`; }

  function evalTop(p) {
    const a=[13,11,28], b=[28,22,56], c=[26,62,46], d=[46,108,88], e=[78,178,240], f=[130,218,255];
    if (p < 0.28) return lerpRGB(a, b, p / 0.28);
    if (p < 0.48) return lerpRGB(b, c, (p - 0.28) / 0.20);
    if (p < 0.68) return lerpRGB(c, d, (p - 0.48) / 0.20);
    if (p < 0.86) return lerpRGB(d, e, (p - 0.68) / 0.18);
    return lerpRGB(e, f, (p - 0.86) / 0.14);
  }
  function evalBot(p) {
    const a=[24,18,46], b=[44,30,74], c=[42,92,58], d=[74,148,108], e=[156,224,255], f=[218,246,255];
    if (p < 0.28) return lerpRGB(a, b, p / 0.28);
    if (p < 0.48) return lerpRGB(b, c, (p - 0.28) / 0.20);
    if (p < 0.68) return lerpRGB(c, d, (p - 0.48) / 0.20);
    if (p < 0.86) return lerpRGB(d, e, (p - 0.68) / 0.18);
    return lerpRGB(e, f, (p - 0.86) / 0.14);
  }

  function heightProgress(y) {
    return Math.max(0, Math.min(1, 1 - y / WORLD_H));
  }
  function zoneName(p) {
    if (p < 0.34) return "THE DEEP CHASM";
    if (p < 0.67) return "OVERGROWN FOREST";
    return "THE BRIGHT SKIES";
  }

  function drawWorld() {
    let sx = 0, sy = 0;
    if (shakeTimer > 0) {
      sx = (Math.random() - 0.5) * 6;
      sy = (Math.random() - 0.5) * 6;
    }
    const cx = Math.round(camX + sx);
    const cy = Math.round(camY + sy);

    const prog = heightProgress(cy + 200);
    const topC = evalTop(prog), botC = evalBot(prog);
    const bands = 25, bh = 400 / bands;
    for (let i = 0; i < bands; i++) {
      wctx.fillStyle = rgbStr(lerpRGB(topC, botC, i / (bands - 1)));
      wctx.fillRect(0, i * bh, VIRTUAL_W, bh + 1);
    }

    const ruinFade = Math.max(0, Math.min(1, 1 - Math.max(0, prog - 0.30) / 0.22));
    const forestFade = Math.max(0, Math.min(1, 1 - Math.abs(prog - 0.50) / 0.28));
    const skyFade = Math.max(0, Math.min(1, (prog - 0.52) / 0.24));

    if (ruinFade > 0.01) {
      wctx.fillStyle = `rgba(26,22,48,${0.85 * ruinFade})`;
      for (let i = -1; i < 8; i++) {
        const px = Math.floor(i * 112 - ((cx * 0.22) % 112));
        wctx.fillRect(px, 0, 28, 400);
        wctx.fillRect(px - 6, 40, 40, 12);
        wctx.fillRect(px - 6, 240, 40, 12);
      }
      for (let i = -1; i < 6; i++) {
        const px = Math.floor(i * 160 - ((cx * 0.40) % 160));
        const py = Math.floor(120 - ((cy * 0.35) % 200));
        wctx.fillStyle = `rgba(36,30,64,${0.90 * ruinFade})`;
        wctx.fillRect(px + 20, 0, 20, 400);
        wctx.globalAlpha = (0.7 + 0.3 * Math.sin(elapsedVisual * 2.2 + i * 1.4)) * ruinFade;
        wctx.drawImage(glowCyan, px + 12, (py + 220 + 400) % 400, 36, 36);
        wctx.globalAlpha = 1;
      }
    }

    if (forestFade > 0.01) {
      for (let i = -1; i < 7; i++) {
        const px = Math.floor(i * 130 - ((cx * 0.26) % 130));
        wctx.fillStyle = `rgba(24,52,38,${0.80 * forestFade})`;
        wctx.fillRect(px + 18, 0, 34, 400);
        let ly = Math.floor(40 + ((i * 53 - cy * 0.18) % 140));
        if (ly < 0) ly += 140;
        wctx.fillStyle = `rgba(34,82,52,${0.85 * forestFade})`;
        wctx.fillRect(px - 16, ly, 102, 28);
      }
      wctx.fillStyle = `rgba(172,236,162,${0.12 * forestFade})`;
      for (let i = 0; i < 4; i++) {
        const sxi = Math.floor(60 + i * 170 - ((cx * 0.15) % 170) + Math.sin(elapsedVisual * 0.6 + i) * 8);
        wctx.fillRect(sxi, 0, 44, 400);
      }
    }

    if (skyFade > 0.01) {
      wctx.globalAlpha = skyFade;
      wctx.drawImage(glowGold, 460, 18, 128, 128);
      wctx.globalAlpha = 1;
      for (let i = 0; i < 7; i++) {
        const drift = (elapsedVisual * (6 + (i % 3) * 3)) % 760;
        const clx = ((i * 135 + drift - cx * 0.18) % 760) - 100;
        let cly = (i * 68 - cy * 0.22) % 420;
        if (cly < 0) cly += 420;
        const cw = 72 + (i % 3) * 24, ch = 18 + (i % 2) * 6;
        wctx.fillStyle = `rgba(224,244,255,${0.65 * skyFade})`;
        wctx.fillRect(Math.floor(clx), Math.floor(cly), cw, ch);
        wctx.fillStyle = `rgba(250,253,255,${0.85 * skyFade})`;
        wctx.fillRect(Math.floor(clx) + 12, Math.floor(cly) - 6, cw - 24, 10);
      }
    }

    // Ambient motes
    const moteRgb = prog < 0.36 ? "128,224,255" : (prog < 0.68 ? "196,252,130" : "255,252,232");
    for (const m of motes) {
      const dx = Math.sin(elapsedVisual * 1.1 + m.phase) * 14;
      const ry = (elapsedVisual * m.speed) % 400;
      let mx = (m.x + dx - cx * 0.55) % 640; if (mx < 0) mx += 640;
      let my = (m.y - ry - cy * 0.55) % 400; if (my < 0) my += 400;
      const tw = 0.55 + 0.45 * Math.sin(elapsedVisual * 3 + m.phase * 2);
      wctx.fillStyle = `rgba(${moteRgb},${0.7 * tw})`;
      wctx.fillRect(Math.floor(mx), Math.floor(my), m.size, m.size);
    }

    // Props
    for (const p of DATA.props) {
      const px = p.x - cx, py = p.y - cy;
      if (px < -48 || px > VIRTUAL_W + 48 || py < -80 || py > VIRTUAL_H + 80) continue;
      if (p.k === 0) {
        wctx.globalAlpha = 0.65 + 0.35 * Math.sin(elapsedVisual * 3 + p.x);
        wctx.drawImage(glowCyan, px - 8, py - 14, 28, 28);
        wctx.globalAlpha = 1;
        wctx.fillStyle = "#6ee8ff"; wctx.fillRect(px + 4, py - 8, 4, 8);
        wctx.fillStyle = "#a8f8ff"; wctx.fillRect(px + 8, py - 5, 3, 5);
      } else if (p.k === 1) {
        wctx.globalAlpha = 0.80 + 0.20 * Math.sin(elapsedVisual * 7 + p.y);
        wctx.drawImage(glowAmber, px - 10, py - 18, 32, 32);
        wctx.globalAlpha = 1;
        wctx.fillStyle = "#403430"; wctx.fillRect(px + 6, py - 10, 2, 10);
        wctx.fillStyle = "#ffde76"; wctx.fillRect(px + 5, py - 12, 4, 4);
      } else if (p.k === 2) {
        const sway = Math.sin(elapsedVisual * 1.8 + p.x * 0.1) * 1.5;
        const h = p.l * TILE_SIZE;
        wctx.fillStyle = "#2c8444";
        for (let vy = 0; vy < h; vy += 4) {
          wctx.fillRect(Math.floor(px + 7 + sway * (vy / h)), Math.floor(py + vy), 2, 4);
        }
      }
    }

    // Visible tiles
    const minTx = Math.max(0, Math.floor(cx / TILE_SIZE) - 1);
    const maxTx = Math.min(DATA.width - 1, Math.ceil((cx + VIRTUAL_W) / TILE_SIZE) + 1);
    const minTy = Math.max(0, Math.floor(cy / TILE_SIZE) - 1);
    const maxTy = Math.min(DATA.height - 1, Math.ceil((cy + VIRTUAL_H) / TILE_SIZE) + 1);
    for (let ty = minTy; ty <= maxTy; ty++) {
      for (let tx = minTx; tx <= maxTx; tx++) {
        const t = getTile(tx, ty);
        if (t === 0) continue;
        const row = Math.abs(tx + ty) % 2;
        wctx.drawImage(tileAtlas, t * 16, row * 16, 16, 16, tx * 16 - cx, ty * 16 - cy, 16, 16);
      }
    }

    // Sky Escape Gate
    const ax = DATA.goalArchBase.x - cx, ay = DATA.goalArchBase.y - cy;
    if (ay > -64 && ay < VIRTUAL_H + 80) {
      wctx.globalAlpha = 0.78 + 0.22 * Math.sin(elapsedVisual * 4);
      wctx.drawImage(glowCyan, ax - 36, ay - 62, 72, 72);
      wctx.globalAlpha = 1;
      wctx.drawImage(goalArch, ax - 24, ay - 56);
    }

    // Coins
    const cFrame = Math.floor(elapsedVisual * 7) % 4;
    for (const c of coins) {
      if (c.collected) continue;
      const bob = Math.sin(elapsedVisual * 3.8 + c.x * 0.08) * 2;
      const sxp = Math.floor(c.x - cx), syp = Math.floor(c.y + bob - cy);
      if (sxp < -16 || sxp > VIRTUAL_W + 16 || syp < -16 || syp > VIRTUAL_H + 16) continue;
      if (c.v >= 3) wctx.drawImage(c.v >= 5 ? glowCyan : glowGold, sxp - 10, syp - 10, 20, 20);
      const cf = (cFrame + c.id) % 4;
      wctx.drawImage(coinAtlas, cf * 12, c.t * 12, 12, 12, sxp - 6, syp - 6, 12, 12);
    }

    // Particles
    for (const p of particles) {
      wctx.globalAlpha = Math.max(0, Math.min(1, p.life / p.maxLife));
      wctx.fillStyle = p.color;
      wctx.fillRect(Math.floor(p.x - cx), Math.floor(p.y - cy), p.size, p.size);
    }
    wctx.globalAlpha = 1;

    // Player
    let pFrame = 0;
    if (!player.onGround) pFrame = player.sustaining ? 7 : (player.vy < 0 ? 5 : 6);
    else if (Math.abs(player.vx) > 22) pFrame = 1 + (Math.floor(player.animTimer) % 4);
    const px = Math.round(player.x - cx), py = Math.round(player.y - cy);
    if (player.sustaining) wctx.drawImage(glowCyan, px - 14, py - 18, 28, 24);
    wctx.save();
    wctx.translate(px, py);
    wctx.scale(player.facingRight ? player.sx : -player.sx, player.sy);
    wctx.drawImage(playerAtlas, pFrame * 16, 0, 16, 16, -8, -15, 16, 16);
    wctx.restore();

    // Popups
    for (const pop of popups) {
      wctx.globalAlpha = Math.max(0, Math.min(1, pop.life / pop.maxLife));
      drawCenteredText(wctx, pop.text, pop.x - cx, pop.y - cy, pop.color, 1, true);
    }
    wctx.globalAlpha = 1;
  }

  function drawHud() {
    const p = heightProgress(player.y);
    const pct = Math.round(p * 100);
    drawBox(ctx, 20, 18, 386, 96, "rgba(14,16,28,0.78)", "#5476a8", 2);
    drawText(ctx, `TIME ${formatTime(runTimer)}`, 36, 30, "#ffffff", 3);
    if (screen !== "Playing") drawText(ctx, "PAUSED", 284, 34, "#8ae8ff", 2);
    const best = save.bestTimeSeconds !== null ? formatTime(save.bestTimeSeconds) : "--:--.--";
    drawText(ctx, `BEST ${best}`, 36, 62, "#ffde60", 2);
    drawText(ctx, `${zoneName(p)} (${pct}%)`, 36, 88, "#8ee0fa", 2);

    const anyAff = stats.canAfford(0) || stats.canAfford(1) || stats.canAfford(2);
    const sBorder = anyAff ? (Math.sin(elapsedVisual * 7) > 0 ? "#ffffff" : "#f8c444") : "#5c769e";
    drawBox(ctx, UI_W - 332, 18, 312, 82, "rgba(14,16,28,0.78)", sBorder, 2);
    drawText(ctx, `COINS: ${stats.coins}`, UI_W - 314, 30, "#ffe262", 3);
    drawDeckBadge(ctx, "Y", UI_W - 314, 62, "#fcce48", 2);
    drawText(ctx, anyAff ? "SHOP UPGRADE READY!" : "OPEN SHOP (TAB)", UI_W - 274, 66, anyAff ? "#92faa2" : "#c4d4e8", 2);

    drawBox(ctx, 20, UI_H - 54, 412, 36, "rgba(14,16,28,0.75)", "#46587c", 2);
    drawText(ctx, `SPD ${stats.levels[0]}/5`, 34, UI_H - 43, "#ffba4a", 2);
    drawText(ctx, `HGT ${stats.levels[1]}/5`, 168, UI_H - 43, "#6ce88a", 2);
    drawText(ctx, `DUR ${stats.levels[2]}/5`, 302, UI_H - 43, "#62e0ff", 2);

    drawBox(ctx, 444, UI_H - 54, 198, 36, "rgba(14,16,28,0.75)", "#46587c", 2);
    drawDeckBadge(ctx, "X", 454, UI_H - 47, "#6cc6ff", 2);
    drawText(ctx, "HOW TO PLAY", 494, UI_H - 43, "#cee0f6", 2);

    // Right altitude gauge
    const bx = UI_W - 30, by = 128, bw = 12, bh = UI_H - 180;
    drawBox(ctx, bx - 2, by - 2, bw + 4, bh + 4, "rgba(10,12,22,0.82)", "#6e92be", 2);
    const third = Math.floor(bh / 3);
    ctx.fillStyle = "#62c6fa"; ctx.fillRect(bx, by, bw, third);
    ctx.fillStyle = "#409e4e"; ctx.fillRect(bx, by + third, bw, third);
    ctx.fillStyle = "#44347c"; ctx.fillRect(bx, by + third * 2, bw, bh - third * 2);
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(bx - 5, by + Math.floor((1 - p) * (bh - 6)), bw + 10, 6);

    if (touchState.enabled && screen === "Playing") {
      const drawTBtn = (x, y, w, h, label, active) => {
        drawBox(ctx, x, y, w, h, active ? "rgba(98,224,255,0.35)" : "rgba(14,18,32,0.55)", "#82c8ff", 2);
        drawCenteredText(ctx, label, x + w / 2, y + h / 2 - 7, "#ffffff", 2);
      };
      drawTBtn(28, UI_H - 155, 120, 80, "<", touchState.left);
      drawTBtn(168, UI_H - 155, 120, 80, ">", touchState.right);
      drawTBtn(UI_W - 365, UI_H - 155, 130, 80, "DROP", touchState.drop);
      drawTBtn(UI_W - 210, UI_H - 155, 155, 80, "JUMP", touchState.jump);
    }
  }

  function drawMainMenu() {
    ctx.fillStyle = "rgba(8,10,22,0.65)";
    ctx.fillRect(0, 0, UI_W, UI_H);
    const boxW = 720, boxH = 520, bx = (UI_W - boxW) / 2, by = (UI_H - boxH) / 2;
    drawBox(ctx, bx, by, boxW, boxH, "rgba(16,18,34,0.95)", "#6ec6fa", 3);
    drawCenteredText(ctx, "ESCAPE TO THE SKIES", UI_W / 2, by + 38, "#8ae4ff", 5);
    drawCenteredText(ctx, "CLIMB FROM THE DEEP CHASM TO THE SUNLIT SUMMIT", UI_W / 2, by + 96, "#c4d6ec", 2);
    const bestLine = save.bestTimeSeconds !== null
      ? `PERSONAL BEST: ${formatTime(save.bestTimeSeconds)}   |   BEST COINS: ${save.bestCoinsCollected}`
      : "COLLECT COINS. UPGRADE 3 STATS. BEAT YOUR BEST TIME.";
    drawCenteredText(ctx, bestLine, UI_W / 2, by + 136, "#ffe060", 2);

    const items = [
      "START CLIMB",
      "HOW TO PLAY",
      document.fullscreenElement ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
      audio.muted ? "SOUND: MUTED" : "SOUND: ON",
    ];
    for (let i = 0; i < items.length; i++) {
      const sel = i === mainMenuIdx, r = getMainMenuItemRect(i);
      drawBox(ctx, r.x, r.y, r.w, r.h, sel ? "#223c52" : "rgba(22,26,46,0.9)", sel ? "#76ecb6" : "#485c86", sel ? 3 : 2);
      if (sel) drawText(ctx, ">", r.x + 18, r.y + 13, "#8af8c4", 2);
      drawCenteredText(ctx, items[i], UI_W / 2, r.y + 14, sel ? "#ffffff" : "#ccdaee", 2);
    }

    const footY = by + boxH - 52;
    let fx = bx + 90;
    drawDeckBadge(ctx, "D-PAD", fx, footY, "#accef4", 2); fx += measureBadgeWidth("D-PAD", 2) + 8;
    drawText(ctx, "Select", fx, footY + 4, "#ffffff", 2); fx += 116;
    drawDeckBadge(ctx, "A", fx, footY, "#60e080", 2); fx += measureBadgeWidth("A", 2) + 8;
    drawText(ctx, "Confirm", fx, footY + 4, "#ffffff", 2); fx += 130;
    drawDeckBadge(ctx, "X", fx, footY, "#6cc6ff", 2); fx += measureBadgeWidth("X", 2) + 8;
    drawText(ctx, "How To Play", fx, footY + 4, "#ffffff", 2);
  }

  function drawShop() {
    ctx.fillStyle = "rgba(8,10,20,0.76)";
    ctx.fillRect(0, 0, UI_W, UI_H);
    const panelW = 900, panelH = 620, px = (UI_W - panelW) / 2, py = (UI_H - panelH) / 2;
    drawBox(ctx, px, py, panelW, panelH, "rgba(18,22,38,0.96)", "#74b2e8", 2);
    drawText(ctx, "STAT UPGRADE SHOP", px + 34, py + 28, "#ebf6ff", 4);
    drawBox(ctx, px + 454, py + 28, 172, 34, "#1c2a3e", "#80e0ff", 2);
    drawCenteredText(ctx, "TIMER PAUSED", px + 540, py + 37, "#a8f2ff", 2);

    const coinLabel = `COINS: ${stats.coins}`;
    const csz = measureText(coinLabel, 3);
    drawBox(ctx, px + panelW - csz.w - 66, py + 24, csz.w + 34, 42, "rgba(34,28,16,0.95)", "#f8cc44", 2);
    drawText(ctx, coinLabel, px + panelW - csz.w - 49, py + 34, "#ffe060", 3);
    drawText(ctx, "Boost your climb. Reach higher ledges & speedrun to the sky!", px + 34, py + 72, "#acc2da", 2);

    const env = simulateMaxJump(stats.levels[0], stats.levels[1], stats.levels[2]);
    drawCenteredText(ctx, `CURRENT PEAK: ${(env.maxHeightPx / TILE_SIZE).toFixed(1)} TILES HIGH   |   MAX LEAP: ${(env.maxDistancePx / TILE_SIZE).toFixed(1)} TILES WIDE`,
      px + panelW / 2, py + 104, "#8ee2fa", 2);

    for (let i = 0; i < 3; i++) {
      const st = STAT_ORDER[i], meta = STAT_META[st], cy = py + 140 + i * 138, cx = px + 34, cw = 832, ch = 122;
      const sel = i === shopIdx, lv = stats.levels[st], maxed = stats.isMaxed(st), cost = stats.getCost(st), aff = stats.canAfford(st);
      let border = sel ? meta.accent : "#424e70";
      if (sel && shopFlashTimer > 0) border = shopFlashErr ? "#ff5c5c" : "#ffffff";
      drawBox(ctx, cx, cy, cw, ch, sel ? "rgba(30,38,64,0.98)" : "rgba(22,26,44,0.92)", border, sel ? 3 : 2);
      if (sel) drawText(ctx, ">", cx + 14, cy + 20, meta.accent, 3);
      drawText(ctx, meta.name, cx + 42, cy + 16, meta.accent, 3);
      drawText(ctx, meta.desc, cx + 42, cy + 48, "#c0cee4", 2);

      for (let p = 0; p < S.maxLevel; p++) {
        const filled = p < lv, prev = p === lv && sel && !maxed;
        drawBox(ctx, cx + 42 + p * 34, cy + 78, 26, 20,
          filled ? meta.accent : (prev ? "rgba(180,220,255,0.28)" : "#2e3650"),
          filled ? "#c8d8ec" : "#4c5878", 2);
      }
      const vNow = meta.fmt(lv);
      drawText(ctx, maxed ? `${vNow} (MAX)` : `${vNow} -> ${meta.fmt(lv + 1)}`, cx + 234, cy + 81, "#ffffff", 2);

      const cbx = cx + cw - 206, cby = cy + 28;
      if (maxed) {
        drawBox(ctx, cbx, cby, 186, 66, "#1c3028", "#6ee094", 2);
        drawCenteredText(ctx, "MAXED OUT", cbx + 93, cby + 26, "#8ef6ae", 2);
      } else {
        drawBox(ctx, cbx, cby, 186, 66, aff ? "#2e2812" : "#24181e", aff ? "#fad048" : "#804a54", 2);
        drawCenteredText(ctx, `${cost} COINS`, cbx + 93, cby + 14, aff ? "#ffe466" : "#dc7e86", 2);
        drawCenteredText(ctx, aff ? "UPGRADE" : "NEED COINS", cbx + 93, cby + 38, aff ? "#bef8ae" : "#a8767e", 2);
      }
    }

    const footY = py + panelH - 54;
    let fx = px + 36;
    drawDeckBadge(ctx, "D-PAD", fx, footY, "#b4cae8", 2); fx += measureBadgeWidth("D-PAD", 2) + 10;
    drawText(ctx, "Select", fx, footY + 4, "#ffffff", 2); fx += 116;
    drawDeckBadge(ctx, "A", fx, footY, "#60e080", 2); fx += measureBadgeWidth("A", 2) + 10;
    drawText(ctx, "Buy Upgrade", fx, footY + 4, "#ffffff", 2); fx += 180;
    drawDeckBadge(ctx, "Y", fx, footY, "#fcce48", 2); fx += measureBadgeWidth("Y", 2) + 8;
    drawDeckBadge(ctx, "B", fx, footY, "#f26666", 2); fx += measureBadgeWidth("B", 2) + 10;
    drawText(ctx, "Resume (Tab/Esc)", fx, footY + 4, "#ffffff", 2);
  }

  function drawHowToPlay() {
    ctx.fillStyle = "rgba(8,10,20,0.80)";
    ctx.fillRect(0, 0, UI_W, UI_H);
    const panelW = 980, panelH = 650, px = (UI_W - panelW) / 2, py = (UI_H - panelH) / 2;
    drawBox(ctx, px, py, panelW, panelH, "rgba(16,20,36,0.96)", "#74c6fa", 3);
    drawText(ctx, "HOW TO PLAY", px + 34, py + 26, "#8ae6ff", 4);
    if (howToPlayReturnTo !== "MainMenu") {
      drawBox(ctx, px + panelW - 252, py + 26, 218, 36, "#1c2a3e", "#80e0ff", 2);
      drawCenteredText(ctx, "TIMER PAUSED", px + panelW - 143, py + 36, "#a8f2ff", 2);
    }

    drawBox(ctx, px + 34, py + 82, panelW - 68, 92, "rgba(22,28,50,0.92)", "#4c6694", 2);
    drawText(ctx, "GOAL: ESCAPE TO THE SKIES", px + 48, py + 96, "#ffe060", 2);
    drawText(ctx, "Start deep underground in dark stone ruins, climb through the forest canopy,\nand reach the Sky Escape Gate at the top in your fastest time!", px + 48, py + 124, "#d0e0f4", 2);

    drawBox(ctx, px + 34, py + 188, panelW - 68, 224, "rgba(22,28,50,0.92)", "#4c6694", 2);
    drawText(ctx, "STEAM DECK & KEYBOARD CONTROLS", px + 48, py + 202, "#8eeab4", 2);
    const lines = [
      ["L-STICK / D-PAD", "Move Left & Right (Up does not jump)", "A / D / ARROWS", "#accef4"],
      ["A BUTTON", "Jump (Hold for lift; Down+A drops)", "SPACE / Z", "#60e080"],
      ["Y BUTTON", "Open/Close Shop (Timer Paused)", "TAB / E", "#fcce48"],
      ["X BUTTON", "How To Play (Timer Paused)", "H / F1", "#6cc6ff"],
      ["MENU / VIEW", "Pause Menu  /  Quick Restart Run", "ESC / R", "#c4b0f8"],
    ];
    lines.forEach(([b, d, k, c], idx) => {
      const y = py + 234 + idx * 34;
      drawDeckBadge(ctx, b, px + 48, y, c, 2);
      drawText(ctx, d, px + 48 + 208, y + 4, "#ffffff", 2);
      drawText(ctx, `(${k})`, px + 48 + 688, y + 4, "#94a8c2", 2);
    });

    drawBox(ctx, px + 34, py + 426, panelW - 68, 144, "rgba(22,28,50,0.92)", "#4c6694", 2);
    drawText(ctx, "COLLECT COINS & UPGRADE 3 STATS", px + 48, py + 440, "#ffca5c", 2);
    drawText(ctx, "1. MOVE SPEED:     Run faster & leap across wider horizontal gaps", px + 48, py + 474, "#ffba4a", 2);
    drawText(ctx, "2. JUMP HEIGHT:    Higher initial jump spring to reach taller ledges", px + 48, py + 502, "#6ce88a", 2);
    drawText(ctx, "3. JUMP DURATION:  Hold Jump longer for extended skyward boost", px + 48, py + 530, "#62e0ff", 2);

    const footY = py + panelH - 54;
    let fx = px + 34;
    drawDeckBadge(ctx, "A", fx, footY, "#60e080", 2); fx += measureBadgeWidth("A", 2) + 8;
    drawDeckBadge(ctx, "B", fx, footY, "#f26666", 2); fx += measureBadgeWidth("B", 2) + 8;
    drawDeckBadge(ctx, "X", fx, footY, "#6cc6ff", 2); fx += measureBadgeWidth("X", 2) + 12;
    drawText(ctx, "Close & Return (H / Esc / Enter)", fx, footY + 4, "#ffffff", 2);
  }

  function drawPaused() {
    ctx.fillStyle = "rgba(6,8,16,0.75)";
    ctx.fillRect(0, 0, UI_W, UI_H);
    const boxW = 480, boxH = 430, bx = (UI_W - boxW) / 2, by = (UI_H - boxH) / 2;
    drawBox(ctx, bx, by, boxW, boxH, "rgba(18,22,38,0.96)", "#78b6f0", 3);
    drawCenteredText(ctx, "PAUSED", UI_W / 2, by + 28, "#ffffff", 4);
    const items = [
      "RESUME CLIMB",
      "HOW TO PLAY",
      "RESTART RUN",
      document.fullscreenElement ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
      audio.muted ? "SOUND: MUTED" : "SOUND: ON",
      "MAIN MENU",
    ];
    for (let i = 0; i < items.length; i++) {
      const sel = i === pauseIdx, r = getPauseItemRect(i);
      if (sel) drawBox(ctx, r.x, r.y, r.w, r.h, "#26365c", "#7adcff", 2);
      drawCenteredText(ctx, items[i], UI_W / 2, r.y + 10, sel ? "#94eeff" : "#ffffff", 2);
    }
    drawCenteredText(ctx, "[A] SELECT   |   [B / ESC] RESUME", UI_W / 2, by + boxH - 34, "#a8bcd6", 2);
  }

  function drawVictory() {
    ctx.fillStyle = "rgba(24,62,108,0.68)";
    ctx.fillRect(0, 0, UI_W, UI_H);
    const boxW = 680, boxH = 500, bx = (UI_W - boxW) / 2, by = (UI_H - boxH) / 2;
    drawBox(ctx, bx, by, boxW, boxH, "rgba(16,26,46,0.95)", "#ffde62", 3);
    drawCenteredText(ctx, "ESCAPED TO THE SKIES!", UI_W / 2, by + 36, "#8aeaff", 4);
    if (newRecord) drawCenteredText(ctx, "NEW PERSONAL BEST TIME!", UI_W / 2, by + 84, "#ffe25e", 3);
    const tStr = formatTime(runTimer);
    drawCenteredText(ctx, tStr, UI_W / 2, by + 132, "#ffffff", 6);
    const bStr = save.bestTimeSeconds !== null ? formatTime(save.bestTimeSeconds) : tStr;
    drawCenteredText(ctx, `BEST RECORD: ${bStr}`, UI_W / 2, by + 196, "#ffdc66", 2);

    drawBox(ctx, bx + 64, by + 236, boxW - 128, 118, "rgba(22,34,58,0.90)", "#6098d0", 2);
    drawCenteredText(ctx, `COINS COLLECTED: ${stats.totalCoinsCollected} / ${totalPossibleCoins}`, UI_W / 2, by + 256, "#ffffff", 2);
    drawCenteredText(ctx, `UPGRADES PURCHASED: ${stats.upgradesPurchased}`, UI_W / 2, by + 286, "#b8e0f8", 2);
    drawCenteredText(ctx, `SPEED ${stats.levels[0]}/5   |   HEIGHT ${stats.levels[1]}/5   |   DURATION ${stats.levels[2]}/5`, UI_W / 2, by + 318, "#8ef0b4", 2);

    const btns = ["RUN AGAIN [A]", "MAIN MENU [B]"];
    for (let i = 0; i < 2; i++) {
      const sel = i === victoryIdx, r = getVictoryBtnRect(i);
      drawBox(ctx, r.x, r.y, r.w, r.h, sel ? "#24483e" : "#182236", sel ? "#76f69c" : "#5c7aa0", sel ? 3 : 2);
      drawCenteredText(ctx, btns[i], r.x + r.w / 2, r.y + 20, "#ffffff", 2);
    }
    drawCenteredText(ctx, "[D-PAD / CLICK] CHOOSE  |  [A] CONFIRM  |  [B] MAIN MENU", UI_W / 2, by + boxH - 34, "#a8c6e0", 2);
  }

  function render() {
    drawWorld();
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(worldCanvas, 0, 0, VIRTUAL_W, VIRTUAL_H, 0, 0, UI_W, UI_H);

    if (screen === "MainMenu") drawMainMenu();
    else if (screen === "Playing") drawHud();
    else if (screen === "Shop") { drawHud(); drawShop(); }
    else if (screen === "HowToPlay") { if (howToPlayReturnTo !== "MainMenu") drawHud(); drawHowToPlay(); }
    else if (screen === "Paused") { drawHud(); drawPaused(); }
    else if (screen === "Victory") drawVictory();
  }

  let lastTime = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, Math.max(0, (now - lastTime) / 1000));
    lastTime = now;
    update(dt);
    render();
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})();
