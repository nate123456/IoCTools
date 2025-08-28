namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Framework;

// This service derives from FrameworkBase and will be skipped by default via .editorconfig in this sample
[Scoped]
public partial class FrameworkDerivedService : FrameworkBase, IFrameworkDerivedService
{
}

public interface IFrameworkDerivedService
{
}
