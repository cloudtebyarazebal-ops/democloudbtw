using System.Security.Cryptography;
using System.Text;

namespace ExamCoachDesktop;

public static class SeedSignatureHelper
{
    public static string Compute(AssignmentSeedData seed)
    {
        var sb = new StringBuilder();
        sb.Append(seed.UseAuthorField ? "A" : "M");
        sb.Append('|').Append(seed.ProductWord);
        sb.Append('|').AppendJoin(',', seed.Categories);
        sb.Append('|').AppendJoin(',', seed.Makers);
        sb.Append('|').AppendJoin(',', seed.Suppliers);
        foreach (var p in seed.Products)
            sb.Append('|').Append(p.Article).Append(':').Append(p.Name);
        sb.Append('|').Append(seed.DiscountHighlightPercent);
        sb.Append('|').Append(seed.DiscountHighlightColor);
        sb.Append('|').Append(seed.OrdersEnabled);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16];
    }
}
