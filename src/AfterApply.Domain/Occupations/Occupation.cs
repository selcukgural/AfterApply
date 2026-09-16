using AfterApply.Domain.Common;

namespace AfterApply.Domain.Occupations;

/// <summary>
/// One row of the occupation catalogue: an ISCO-08 unit group, or a market job title mapped to
/// one. Seeded by migration from a versioned CSV, never written by a user — a salary entry may
/// only point at a row that is here, which is what keeps "Sr. Backend Dev" and "Senior Backend
/// Developer" from being two occupations. Rows are retired with <see cref="IsActive"/>, never
/// deleted: salary entries reference them.
/// </summary>
public sealed class Occupation : Entity
{
    public const int MaxCodeLength = 16;
    public const int MaxNameLength = 120;
    public const int Isco08CodeLength = 4;

    /// <summary>The ISCO-08 unit group ("2512") or a curated code ("EK-0001"). Unique.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>The ISCO-08 unit group this row belongs to; equals <see cref="Code"/> on an ISCO row.</summary>
    public string Isco08Code { get; private set; } = string.Empty;

    public string NameTr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    /// <summary>Search columns — see <see cref="OccupationName.Normalize"/>.</summary>
    public string NormalizedNameTr { get; private set; } = string.Empty;

    public string NormalizedNameEn { get; private set; } = string.Empty;

    public OccupationSource Source { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Occupation()
    {
    }

    /// <summary>The id is derived from the code so every environment — and every re-run of the
    /// seed — produces the same row for the same occupation.</summary>
    public static Occupation Create(string code, string isco08Code, string nameTr, string nameEn, OccupationSource source)
    {
        var occupation = new Occupation { Id = IdFor(code) };
        occupation.Set(code, isco08Code, nameTr, nameEn, source);
        return occupation;
    }

    public void Retire() => IsActive = false;

    private void Set(string code, string isco08Code, string nameTr, string nameEn, OccupationSource source)
    {
        Code = code.Trim();
        Isco08Code = isco08Code.Trim();
        NameTr = nameTr.Trim();
        NameEn = nameEn.Trim();
        NormalizedNameTr = OccupationName.Normalize(NameTr);
        NormalizedNameEn = OccupationName.Normalize(NameEn);
        Source = source;
    }

    /// <summary>A name-based UUID (RFC 4122 §4.3 shape, SHA-1 over a fixed namespace + the code).</summary>
    public static Guid IdFor(string code)
    {
        var namespaceBytes = Namespace.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(code.Trim());
        var hash = System.Security.Cryptography.SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        var result = new byte[16];
        Array.Copy(hash, result, 16);
        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant
        SwapGuidByteOrder(result);
        return new Guid(result);
    }

    // A fixed namespace for e-kariyerim occupation codes; changing it would re-key every row.
    private static readonly Guid Namespace = new("a6f0c1a2-3b4d-4e5f-9a1b-2c3d4e5f6a7b");

    private static void SwapGuidByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
