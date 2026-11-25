using System.Text.RegularExpressions;

namespace TravelPlanner.Api.Services;

public sealed partial class FixedOtpSettings
{
    private FixedOtpSettings() { }

    internal string? Phone { get; private init; }
    internal string? Code { get; private init; }
    internal bool Enabled => Phone is not null;

    internal static FixedOtpSettings Read(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Auth:FixedOtpEnabled"))
            return new FixedOtpSettings();

        var phone = configuration["Auth:FixedOtpPhone"]?.Trim();
        var code = configuration["Auth:FixedOtpCode"]?.Trim();
        if (phone is null || !IndianPhone().IsMatch(phone) || code is null || !SixDigits().IsMatch(code))
            throw new InvalidOperationException(
                "Fixed OTP requires Auth:FixedOtpPhone in +91 format and a six-digit Auth:FixedOtpCode.");

        return new FixedOtpSettings { Phone = phone, Code = code };
    }

    [GeneratedRegex(@"\A\+91[6-9][0-9]{9}\z")]
    private static partial Regex IndianPhone();

    [GeneratedRegex(@"\A[0-9]{6}\z")]
    private static partial Regex SixDigits();
}
