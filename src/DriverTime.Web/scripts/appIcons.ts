import { deflateSync } from "node:zlib";

function crc32(data: Uint8Array) {
    let crc = 0xffffffff;
    for (const value of data) {
        crc ^= value;
        for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
    return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type: string, data: Uint8Array) {
    const bytes = Buffer.concat([Buffer.from(type), data]);
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(bytes));
    return Buffer.concat([length, bytes, crc]);
}

// Build-time, code-native DT icon: no image dependency or external service.
export function createAppIcon(size: number): Uint8Array {
    if (size !== 192 && size !== 512) throw new Error("Unsupported app icon size");
    const letters = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"];
    const letterT = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"];
    const rows = Buffer.alloc(size * (size * 4 + 1));
    for (let y = 0; y < size; y++) {
        for (let x = 0; x < size; x++) {
            const px = x * 64 / size;
            const py = y * 64 / size;
            const glyphY = Math.floor((py - 18) / 4);
            const glyphX = Math.floor((px - 10) / 4);
            const glyph = glyphX >= 0 && glyphX < 5 ? letters
                : glyphX >= 6 && glyphX < 11 ? letterT : null;
            const column = glyphX < 5 ? glyphX : glyphX - 6;
            const white = glyph && glyphY >= 0 && glyphY < 7 && glyph[glyphY][column] === "1";
            const offset = y * (size * 4 + 1) + 1 + x * 4;
            rows[offset] = white ? 255 : Math.round(37 + (1 - y / size) * 22);
            rows[offset + 1] = white ? 255 : Math.round(71 + (1 - y / size) * 65);
            rows[offset + 2] = white ? 255 : 220;
            rows[offset + 3] = 255;
        }
    }
    const header = Buffer.alloc(13);
    header.writeUInt32BE(size, 0);
    header.writeUInt32BE(size, 4);
    header[8] = 8;
    header[9] = 6;
    return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", header),
        chunk("IDAT", deflateSync(rows)), chunk("IEND", Buffer.alloc(0))]);
}
