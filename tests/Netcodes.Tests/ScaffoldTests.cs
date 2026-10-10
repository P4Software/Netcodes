namespace Netcodes.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Core_assembly_is_wired_into_the_test_project()
    {
        Assert.Equal("Netcodes", NetcodesInfo.ProductName);
    }
}
