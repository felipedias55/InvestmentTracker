import { createRequestId } from './request-id';
import { vi } from 'vitest';
describe('createRequestId', () => {
  afterEach(() => vi.unstubAllGlobals());
  it('creates a UUID v4 on HTTP when randomUUID is unavailable', () => {
    vi.stubGlobal('crypto', { getRandomValues: (bytes: Uint8Array) => { bytes.fill(255); return bytes; } });
    expect(createRequestId()).toBe('ffffffff-ffff-4fff-bfff-ffffffffffff');
  });
  it('uses cryptographic random bytes for each request', () => {
    expect(createRequestId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(createRequestId()).not.toBe(createRequestId());
  });
});
