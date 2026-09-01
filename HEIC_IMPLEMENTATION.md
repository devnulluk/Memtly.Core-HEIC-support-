# HEIC/HEIF browser support

Implementation branch: `feature/heic-originals`.

Design goals:

- Preserve every uploaded HEIC/HEIF file byte-for-byte as the original/master asset.
- Generate browser-compatible derivatives server-side; never replace or re-encode the original.
- Keep existing JPEG/PNG/video behaviour unchanged.
- Use the derivative for gallery/lightbox display while original-download continues to return the uploaded file.
- Generate derivatives once during ingestion rather than on each request.

## Decoder

HEIC/HEIF decoding is provided by `heif-convert` (libheif) in the Community runtime image. The decoded temporary image is passed through the existing SkiaSharp resize/orientation/WebP pipeline.

The implementation must fail safely: if HEIF decoding fails, preserve the original and use the existing broken-image fallback rather than modifying or deleting the source.

## Compatibility testing

Test at minimum:

1. ordinary iPhone HEIC;
2. portrait/rotated HEIC;
3. HDR iPhone HEIC;
4. HEIC containing auxiliary/depth images;
5. Live Photo still image;
6. JPEG/PNG regression;
7. original download SHA-256 before/after processing.

Note: libheif versions should be tested with current iPhone files because decoder regressions have occurred upstream. Pinning or minimum-version checks may be appropriate once the runtime image is validated.
