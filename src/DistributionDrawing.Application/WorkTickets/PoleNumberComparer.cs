namespace DistributionDrawing.Application.WorkTickets;

internal enum PoleNumberOrder
{
    Unresolved,
    Less,
    Equal,
    Greater
}

/// <summary>
/// Compares the trailing numeric part only when both pole numbers have the same
/// non-numeric prefix. Formats with multiple numeric segments remain unresolved.
/// </summary>
internal static class PoleNumberComparer
{
    public static PoleNumberOrder Compare(string? left, string? right)
    {
        if (!TrySplit(left, out string leftPrefix, out string leftDigits) ||
            !TrySplit(right, out string rightPrefix, out string rightDigits) ||
            !string.Equals(leftPrefix, rightPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return PoleNumberOrder.Unresolved;
        }

        string normalizedLeft = NormalizeDigits(leftDigits);
        string normalizedRight = NormalizeDigits(rightDigits);
        int lengthComparison = normalizedLeft.Length.CompareTo(normalizedRight.Length);
        if (lengthComparison != 0)
            return lengthComparison < 0 ? PoleNumberOrder.Less : PoleNumberOrder.Greater;

        int numericComparison = string.CompareOrdinal(normalizedLeft, normalizedRight);
        return numericComparison switch
        {
            < 0 => PoleNumberOrder.Less,
            > 0 => PoleNumberOrder.Greater,
            _ => PoleNumberOrder.Equal
        };
    }

    private static bool TrySplit(string? value, out string prefix, out string digits)
    {
        prefix = "";
        digits = "";
        if (string.IsNullOrWhiteSpace(value)) return false;

        string candidate = value.Trim();
        if (candidate.EndsWith('#')) candidate = candidate[..^1];

        int digitStart = candidate.Length;
        while (digitStart > 0 && char.IsAsciiDigit(candidate[digitStart - 1]))
            digitStart--;
        if (digitStart == candidate.Length) return false;

        string candidatePrefix = candidate[..digitStart];
        if (candidatePrefix.Any(character => char.IsAsciiDigit(character) ||
                char.IsWhiteSpace(character) || character == '#'))
        {
            return false;
        }

        prefix = candidatePrefix;
        digits = candidate[digitStart..];
        return true;
    }

    private static string NormalizeDigits(string digits)
    {
        string normalized = digits.TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }
}
