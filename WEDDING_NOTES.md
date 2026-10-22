# Wedding integration

This branch inherits `feature/heic-originals` and adds bounded decoder execution and isolated temporary-directory cleanup. It does not merge into master.

The Community wedding portal stores original bytes privately, separate from the stock public gallery directories. Its authenticated media endpoints use the Core thumbnail generator; a decoding failure leaves the original intact and downloadable to an administrator.

The display WebP is a viewing derivative, not an archival substitute. This implementation does **not** claim HDR/gain-map fidelity, preservation of HDR in WebP, Live Photo video/still pairing, or support for every multi-image HEIF container. The original file preserves the bytes actually received; an iOS photo picker can convert an asset before upload. Export an unmodified original to Files and upload that when byte-for-byte preservation from the photo library is essential.
