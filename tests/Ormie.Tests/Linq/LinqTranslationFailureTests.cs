using System.Linq.Expressions;
using Ormie.Linq;
using Ormie.Mapping;
using Ormie.Tests.Entities;

namespace Ormie.Tests.Linq;

public class LinqTranslationFailureTests
{
    [Fact]
    public void Translate_throws_for_unsupported_method_call()
    {
        var map = EntityMapper.Map<User>();
        var predicate = (Expression<Func<User, bool>>)(user => user.GetHashCode() > 0);
        var translator = new SqlExpressionTranslator(map);

        Assert.Throws<LinqTranslationException>(() =>
        {
            translator.Translate(predicate.Body, predicate.Parameters[0]);
        });
    }
}
