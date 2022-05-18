using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvatarClothingHelper
{
    internal struct Blendshape
    {
        public readonly IField<float> Field;
        public readonly string Name;
        public readonly bool Primary;

        public Blendshape(string name, IField<float> field, bool primary)
        {
            Name = name;
            Field = field;
            Primary = primary;
        }
    }
}