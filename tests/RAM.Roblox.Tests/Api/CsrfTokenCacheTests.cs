using RAM.Roblox.Api;

namespace RAM.Roblox.Tests.Api;

public class CsrfTokenCacheTests
{
    [Fact]
    public void Get_returns_null_when_unset()
    {
        var cache = new CsrfTokenCache();
        Assert.Null(cache.Get("cookie-A"));
    }

    [Fact]
    public void Set_then_get_returns_token()
    {
        var cache = new CsrfTokenCache();
        cache.Set("cookie-A", "token-1");
        Assert.Equal("token-1", cache.Get("cookie-A"));
    }

    [Fact]
    public void Set_is_keyed_by_cookie_hash()
    {
        var cache = new CsrfTokenCache();
        cache.Set("cookie-A", "tokenA");
        cache.Set("cookie-B", "tokenB");
        Assert.Equal("tokenA", cache.Get("cookie-A"));
        Assert.Equal("tokenB", cache.Get("cookie-B"));
    }

    [Fact]
    public void Invalidate_removes_token()
    {
        var cache = new CsrfTokenCache();
        cache.Set("c", "t");
        cache.Invalidate("c");
        Assert.Null(cache.Get("c"));
    }
}
