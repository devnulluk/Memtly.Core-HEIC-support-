namespace Memtly.Core.Models
{
    public class MediaUploadRequest
    {
        public required string RequestId { get; set; }
        public required string UploadId { get; set; }
        public required int CollectionId { get; set; }
        public required int GalleryId { get; set; }
        public string SecretKey { get; set; } = string.Empty;
        public required int ChunkIndex { get; set; }
        public required int TotalChunks { get; set; }
        public required IFormFile File { get; set; }
        public required int FileSize { get; set; }
        public required string FileChecksum { get; set; }
    }

    public class MediaUploadResponse
    {
        public MediaUploadResponse(string requestId, string? uploadId, bool success, bool complete)
        {
            this.RequestId = requestId;
            this.UploadId = uploadId;
            this.Success = success;
            this.Complete = complete;
        }

        public string RequestId { get; set; }
        public string? UploadId { get; set; } = null;
        public bool Success { get; set; } = false;
        public bool Complete { get; set; } = false;
        public List<string>? Errors { get; set; }
    }

    public class MediaUploadSuccessResponse : MediaUploadResponse
    {
        public MediaUploadSuccessResponse(string requestId, string? uploadId)
            : base(requestId, uploadId, success: true, complete: false)
        {
        }
    }

    public class MediaUploadCompleteResponse : MediaUploadResponse
    {
        public MediaUploadCompleteResponse(string requestId, string? uploadId)
            : base(requestId, uploadId, success: true, complete: true)
        {
        }
    }

    public class MediaUploadFailureResponse : MediaUploadResponse
    {
        public MediaUploadFailureResponse(string requestId, string? uploadId, string reason)
            : this(requestId, uploadId, new List<string>() { reason })
        {
        }

        public MediaUploadFailureResponse(string requestId, string? uploadId, List<string> reasons)
            : base(requestId, uploadId, false, false)
        {
            this.Errors = reasons;
        }
    }
}