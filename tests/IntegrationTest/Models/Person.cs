﻿using ProtoBuf;

namespace IntegrationTest.Models
{
   [ProtoContract]
   public class Person
   {
      [ProtoMember(1)]
      public int Id { get; set; }

      [ProtoMember(2)]
      public string Name { get; set; } = default!;
   }
}
