using FluentAssertions;
using Gml4Net.Model;
using Xunit;

namespace Gml4Net.Tests.Model;

public class GmlCoordinateTests
{
    [Fact]
    public void Constructor_With2D_HasDimension2()
    {
        var coord = new GmlCoordinate(10.0, 20.0);

        coord.Dimension.Should().Be(2);
        coord.X.Should().Be(10.0);
        coord.Y.Should().Be(20.0);
        coord.Z.Should().BeNull();
        coord.M.Should().BeNull();
    }

    [Fact]
    public void Constructor_With3D_HasDimension3()
    {
        var coord = new GmlCoordinate(10.0, 20.0, 30.0);

        coord.Dimension.Should().Be(3);
        coord.Z.Should().Be(30.0);
    }

    [Fact]
    public void Constructor_WithM_HasDimension3()
    {
        var coord = new GmlCoordinate(10.0, 20.0, M: 1.0);

        coord.Dimension.Should().Be(3);
        coord.M.Should().Be(1.0);
        coord.Z.Should().BeNull();
    }

    [Fact]
    public void Constructor_With4D_HasDimension4()
    {
        var coord = new GmlCoordinate(10.0, 20.0, 30.0, 1.0);

        coord.Dimension.Should().Be(4);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new GmlCoordinate(10.0, 20.0, 30.0);
        var b = new GmlCoordinate(10.0, 20.0, 30.0);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new GmlCoordinate(10.0, 20.0);
        var b = new GmlCoordinate(10.0, 20.1);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_2DVs3D_AreNotEqual()
    {
        var a = new GmlCoordinate(10.0, 20.0);
        var b = new GmlCoordinate(10.0, 20.0, 0.0);

        a.Should().NotBe(b);
    }
}
