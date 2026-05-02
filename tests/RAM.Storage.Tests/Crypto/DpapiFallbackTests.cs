using System.Text;
using RAM.Storage.Crypto;

namespace RAM.Storage.Tests.Crypto;

public class DpapiFallbackTests
{
    [Fact]
    public void Protect_then_unprotect_roundtrips()
    {
        var dpapi = new DpapiFallback();
        var pt = Encoding.UTF8.GetBytes("legacy ram cookie blob");
        var protectedBytes = dpapi.Protect(pt);
        var recovered = dpapi.TryUnprotect(protectedBytes);
        Assert.NotNull(recovered);
        Assert.Equal(pt, recovered);
    }

    [Fact]
    public void Unprotect_returns_null_on_garbage()
    {
        var dpapi = new DpapiFallback();
        Assert.Null(dpapi.TryUnprotect(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }
}
