using System;
using BaseX;
using FrooxEngine;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.Input;

namespace NeosQuickSetupTools
{
    [Category("MyHamsterIsRich")]
    public class Class1 : Component
    {

        public readonly SyncRef<SkinnedMeshRenderer> skinRef;
        public readonly SyncRef<Slot> otherSlot;
        

        protected override void OnAttach()
        {
            base.OnAttach();
            UniLog.Log("Help I'm attached ! Send someone !");
            UniLog.Log("My Slot Name is : " + Slot.Name);
           
            
        }

        private DynamicValueVariableDriver<T> AddDynVarFor<T>(
            IField<T> field,
            string name,
            Slot s,
            bool forceLink = false)
        {
            DynamicValueVariableDriver<T> dynVariable =
                s.AttachComponent<DynamicValueVariableDriver<T>>();
            dynVariable.VariableName.Value = name;
            dynVariable.Target.Target = field;
            /* If a field is already driven, you can only use ForceLink
             * to replace the current driver.
             * This is rather dangerous, as that breaks previous links
             * and I'm not sure 'Undo' will be able to recover from
             * this.
             */
            if (dynVariable.Target.Target == null)
            {
                if (!forceLink)
                {
                    s.RemoveComponent(dynVariable);
                    dynVariable = null;
                }
                else
                {
                    /* FIXME ! Untested ! */
                    dynVariable.Target.ForceLink(field);
                }
            }
            return dynVariable;
        }

        private DynamicValueVariableDriver<T> AddDynVarFor<T>(
            IField<T> field,
            string name,
            bool forceLink = false)
        {
            return AddDynVarFor(
                field,
                name,
                field.FindNearestParent<Slot>(),
                forceLink);
        }

        void AddCodeTo(Slot slot)
        {

            Slot add_slot = slot.AddSlot("Add");
            Slot int_val0_slot = slot.AddSlot("IntA");
            Slot int_val1_slot = slot.AddSlot("IntB");


            Type typeT = typeof(FrooxEngine.LogiX.Input.IntInput);
            UniLog.Log(null);
            string searchString = typeT.FullName + ",FrooxEngine.LogiX.Input";
            UniLog.Log(
                "Assembly Names : \n" +
                "Full Name : " + typeT.Assembly.FullName  + "\n" +
                "Name      : " + typeT.Assembly.GetName() + "\n");
            UniLog.Log(
                "IntInput :\n" +
                typeT.AssemblyQualifiedName + "\n" +
                typeT.Name + "\n" +
                searchString + "\n");

            
            string receivedType = "Input.FloatInput";
            string searchedType = "FrooxEngine.LogiX." + receivedType + ", " + typeT.Assembly.FullName;
            UniLog.Log("Looking for : " + searchedType);
            UniLog.Log("Instead of  : " + typeof(FloatInput).AssemblyQualifiedName);

            Type retrospecType = Type.GetType(searchedType);
            Type otherRestrospec = Type.GetType(typeT.Name);
            
            if (retrospecType != null)
            {
                Slot test_slot = slot.AddSlot("Test");
                test_slot.AttachComponent(retrospecType);
            }
            else
            {
                UniLog.Log("Retrospec is null !");
            }

            if (otherRestrospec != null)
            {
                UniLog.Log("Got it with the short name !");
            }
            else
            {
                UniLog.Log("otherRetrospec is also null");
            }
            
            var int_val0 = int_val0_slot.AttachComponent<IntInput>();
            var int_val1 = int_val1_slot.AttachComponent<IntInput>();
            var add_int  = add_slot.AttachComponent<Add_Int>();
            var field = add_int.TryGetField("A");
            if (field != null)
            {
                ((FrooxEngine.LogiX.Input<int>)field).Target = int_val0;
            }

            field = add_int.TryGetField("B");
            if (field != null)
            {
                ((FrooxEngine.LogiX.Input<int>)field).Target = int_val1;
            }
            //add_int.A.Target = int_val0;
            //add_int.B.Target = int_val1;
        }


        private void AddScript(Slot baseSlot, string serialized)
        {
            string[] lines = serialized.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                switch (line[0])
                {
                    case 'N':
                        string[] args = line.Split(',');
                        string slot_name = args[1];
                        string slot_class = args[2];
                        break;

                }
            }
        }

        protected override void OnChanges()
        {
            base.OnChanges();
            UniLog.Log("-----------------");
            UniLog.Log("Changed triggered");

            //if (body != null && body.IsValid)
            if (otherSlot != null && otherSlot.Target != null)
            {
                AddCodeTo(otherSlot);
            }
            /*if (skinRef             != null 
                && skinRef.Target   != null
                && otherSlot        != null
                && otherSlot.Target != null)
            {
                UniLog.Log("Skin !");
                UniLog.Log(skinRef);
                
                SkinnedMeshRenderer skin = skinRef.Target;
                //Slot s = otherSlot.Target;
                for (int i = 0; i < skin.BlendShapeCount; i++)
                {
                    string name = skin.BlendShapeName(i);
                    IField<float> blendShape = skin.GetBlendShape(name);
                    AddDynVarFor(blendShape, "blendshape." + name, otherSlot);

                }
            }*/



        }

    }
}
