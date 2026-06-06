using Ormie.Mapping;
using Ormie.Tests.Entities;

namespace Ormie.Tests.Mapping;

public class NotMappedTests
{
    [Fact]
    public void Map_ignores_not_mapped_properties()
    {
        var map = EntityMapper.Map<Post>();

        Assert.Equal(3, map.Properties.Count);
        Assert.DoesNotContain(map.Properties, property => property.Property.Name == nameof(Post.User));
    }
}
