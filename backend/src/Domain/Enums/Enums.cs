namespace Platform.Domain.Enums;

public enum ProductStatus
{
    Draft = 0,
    ImagesUploaded = 1,
    Processing = 2,
    Ready = 3,
    Failed = 4
}

public enum ImageProcessingStatus
{
    Pending = 0,
    Validating = 1,
    Processed = 2,
    Rejected = 3
}

public enum JobStatus
{
    Queued = 0,
    ProcessingImages = 1,
    Generating3D = 2,
    OptimizingModel = 3,
    UploadingModel = 4,
    Completed = 5,
    Failed = 6
}

public enum ViewAngle
{
    Unknown = 0,
    Front = 1,
    Back = 2,
    Left = 3,
    Right = 4,
    Top = 5
}

public enum Plan
{
    Free = 0,
    Starter = 1,
    Growth = 2,
    Enterprise = 3
}
