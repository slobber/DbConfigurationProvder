using System;

namespace DbConfiguration
{
  public class EntityChangeEventArgs : EventArgs
  {
    public object Entity { get; }

    public EntityChangeEventArgs(object entity)
    {
      Entity = entity;
    }
  }
}