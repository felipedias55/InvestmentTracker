export interface AssetImageFrame { zoom: number; x: number; y: number; }
export function assetImageGeometry(width: number, height: number, frame: AssetImageFrame) {
  if (width <= 0 || height <= 0 || width > 16000 || height > 16000 || ![width, height, frame.zoom, frame.x, frame.y].every(Number.isFinite)) throw new Error('dimensions');
  const ratio = Math.min(128 / width, 128 / height) * Math.min(4, Math.max(0.5, frame.zoom));
  const scaledWidth = width * ratio, scaledHeight = height * ratio;
  const travelX = Math.max(128, scaledWidth) / 2, travelY = Math.max(128, scaledHeight) / 2;
  return { width: scaledWidth, height: scaledHeight, x: (128 - scaledWidth) / 2 + Math.max(-100, Math.min(100, frame.x)) / 100 * travelX, y: (128 - scaledHeight) / 2 + Math.max(-100, Math.min(100, frame.y)) / 100 * travelY, travelX, travelY };
}
export function renderAssetImage(source: CanvasImageSource, width: number, height: number, frame: AssetImageFrame = { zoom: 1, x: 0, y: 0 }): string {
  const geometry = assetImageGeometry(width, height, frame);
  const canvas = document.createElement('canvas'); canvas.width = 128; canvas.height = 128;
  const context = canvas.getContext('2d'); if (!context) throw new Error('canvas');
  context.drawImage(source, geometry.x, geometry.y, geometry.width, geometry.height);
  const data = canvas.toDataURL('image/png').split(',')[1]; if (!data || data.length > 349528) throw new Error('size'); return data;
}
export async function normalizeAssetImage(file: File, allowSvg = false): Promise<string> {
  let bitmap: ImageBitmap | undefined; let objectUrl: string | undefined;
  try {
    let source: CanvasImageSource, width: number, height: number;
    if (allowSvg && file.type === 'image/svg+xml') {
      objectUrl = URL.createObjectURL(file); const image = new Image(); image.src = objectUrl; await image.decode(); source = image; width = image.naturalWidth; height = image.naturalHeight;
    } else { bitmap = await createImageBitmap(file); source = bitmap; width = bitmap.width; height = bitmap.height; }
    return renderAssetImage(source, width, height);
  } finally { bitmap?.close(); if (objectUrl) URL.revokeObjectURL(objectUrl); }
}
