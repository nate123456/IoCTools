using IoCTools.Abstractions.Annotations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service]
public class GenericService<T> : IGenericService<T>
{
}