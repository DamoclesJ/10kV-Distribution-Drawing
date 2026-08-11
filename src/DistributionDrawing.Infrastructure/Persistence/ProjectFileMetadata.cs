namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectFileMetadata
{
    public ProjectFileMetadata(string title, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Project title is required.", nameof(title));
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    public string Title { get; }

    public string? Description { get; }
}
