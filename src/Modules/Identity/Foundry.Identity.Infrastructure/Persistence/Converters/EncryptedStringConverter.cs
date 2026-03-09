using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Foundry.Identity.Infrastructure.Persistence.Converters;

public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IDataProtector protector) : base(
            v => v == null ? null : protector.Protect(v),
            v => v == null ? null : protector.Unprotect(v))
    {
    }
}
