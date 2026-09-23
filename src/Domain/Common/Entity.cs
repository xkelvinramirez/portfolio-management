using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Common;

public abstract class Entity
{
    public long Id { get; private init; }

    protected Entity(long id)
    {
        Id = id;
    }

    protected Entity() { }
}
