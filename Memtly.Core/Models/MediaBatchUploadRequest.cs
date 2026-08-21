namespace Memtly.Core.Models
{
    public class MediaBatchUploadRequest
    {
        public required string RequestId { get; set; }
        public required int CollectionId { get; set; }
        public required int GalleryId { get; set; }
        public string SecretKey { get; set; } = string.Empty;
        public required int UploadCount { get; set; }
    }

    public class MediaBatchUploadResponse
    {
        public MediaBatchUploadResponse(string requestId, bool success, bool requiresReview)
        {
            this.RequestId = requestId;
            this.Success = success;
            this.RequiresReview = requiresReview;
        }

        public string RequestId { get; set; }
        public bool Success { get; set; } = false;
        public bool RequiresReview { get; set; } = false;
        public MediaBatchUploadCounters? Counters { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class MediaBatchUploadCounters
    {
        public int Total { get; set; } = 0;
        public int Approved { get; set; } = 0;
        public int Pending { get; set; } = 0;
    }

    public class MediaBatchUploadSuccessResponse : MediaBatchUploadResponse
    {
        public MediaBatchUploadSuccessResponse(string requestId, bool requiresReview, MediaBatchUploadCounters counters)
            : base(requestId, true, requiresReview)
        {
            Counters = counters;
        }
    }

    public class MediaBatchUploadFailureResponse : MediaBatchUploadResponse
    {
        public MediaBatchUploadFailureResponse(string requestId, string reason)
            : this(requestId, new List<string>() { reason })
        {
        }

        public MediaBatchUploadFailureResponse(string requestId, List<string> reasons)
            : base(requestId, false, false)
        {
            this.Errors = reasons;
        }
    }
}