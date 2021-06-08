using System;
using BaseX;
using FrooxEngine;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.Input;
using FrooxEngine.LogiX;

namespace NeosQuickSetupTools
{
    [Category("MyHamsterIsRich")]
    public class Class1 : Component
    {

        public readonly SyncRef<SkinnedMeshRenderer> skinRef;
        public readonly SyncRef<Slot> otherSlot;

        private const string logixMainNamespace = "FrooxEngine.LogiX.";
        private string logixAssemblyName =
            typeof(FrooxEngine.LogiX.Input.IntInput).Assembly.FullName;
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

        LogixNode LogixField(Slot slot, string nodeClassName)
        {
            string searchName =
                logixMainNamespace + nodeClassName + ", " + logixAssemblyName;
            UniLog.Log("LogixField GetType " + searchName);
            Type logixNodeType = Type.GetType(searchName);
            LogixNode ret = null;
            if (logixNodeType != null)
            {
                /* FIXME Check if the type is compatible before casting... */
                ret = (LogixNode) slot.AttachComponent(logixNodeType);
            }
            return ret;
        }

        void LogixConnectInputTo(
            LogixNode toNode,
            string inputName,
            LogixNode fromNode,
            string outputName = null)
        {
            /* FIXME Check if the type is compatible before casting... */
            var input = toNode.TryGetField(inputName);
            
            if (outputName == null)
            {
                ((ISyncRef)input).TrySet(fromNode);
            }
            else
            {
                /* Handle getting output from fromNode */
                ((ISyncRef)input).TrySet(fromNode.GetSyncMember(outputName));
            }
        }

        void LogixConnectImpulse(
            LogixNode fromNode,
            string impulseOutputName,
            LogixNode toNode,
            string toActionName)
        {

            Action toAction = (Action)Delegate.CreateDelegate(
                typeof(Action),
                toNode,
                toActionName);
            /* FIXME Check the type before casting... */
            var fromImpulse = fromNode.TryGetField(impulseOutputName);
            if (fromImpulse != null)
            {
                ((Impulse)fromImpulse).Target = toAction;
            }
        }

        void TestAddCodeWithOutputs(Slot slot)
        {
            Slot add_slot = slot.AddSlot("Add");
            Slot float_val0_slot = slot.AddSlot("FloatA");
            Slot float_val1_slot = slot.AddSlot("FloatB");

            var add_field    = LogixField(add_slot,        "Operators.Add_Float");
            var float0_field = LogixField(float_val0_slot, "Input.FloatInput");
            var float1_field = LogixField(float_val1_slot, "Input.FloatInput");

            LogixConnectInputTo(add_field, "A", float0_field);
            LogixConnectInputTo(add_field, "B", float1_field);

            Slot color_to_HSV_slot = slot.AddSlot("ColorToHSV");
            Slot add_to_v_slot = slot.AddSlot("AddToV");

            var color_to_hsv_field = LogixField(color_to_HSV_slot, "Color.ColorToHSV");
            var add_to_v_field = LogixField(add_to_v_slot, "Operators.Add_Float");

            LogixConnectInputTo(add_to_v_field, "A", color_to_hsv_field, "V");
            LogixConnectInputTo(add_to_v_field, "B", float1_field);

            UniLog.Log("Done !");
            /*((Add_Float)add_to_v_field).A.Target =
                ((FrooxEngine.LogiX.Color.ColorToHSV)color_to_hsv_field).V;*/
            /*UniLog.Log("V: (GetField, GetSyncMember, GetType().GetField, direct)");
            UniLog.Log(color_to_hsv_field.TryGetField("V"));
            UniLog.Log(color_to_hsv_field.GetSyncMember("V"));
            UniLog.Log(color_to_hsv_field.GetType().GetField("V"));
            UniLog.Log(((FrooxEngine.LogiX.Color.ColorToHSV)color_to_hsv_field).V);*/

            //slot.AddSlot("meow").AttachComponent<FrooxEngine.LogiX.ProgramFlow.IfNode>().True;
            var fire_on_true_slot = slot.AddSlot("FireOnTrue");
            var if_slot           = slot.AddSlot("IfSlot");

            var fire_on_true_node =
                fire_on_true_slot.AttachComponent<FrooxEngine.LogiX.ProgramFlow.FireOnTrue>();
            var if_slot_node =
                if_slot.AttachComponent<FrooxEngine.LogiX.ProgramFlow.IfNode>();

            LogixConnectImpulse(fire_on_true_node, "Pulse", if_slot_node, "Run");

            /*UniLog.Log("Getting Impulse fields (TryGetField, GetSyncMember) :");
            UniLog.Log(fire_on_true_node.TryGetField("Pulse"));
            UniLog.Log(fire_on_true_node.GetSyncMember("Pulse"));
            UniLog.Log("Getting the target (Action) :");

            UniLog.Log(if_slot_node.TryGetField("Run"));
            UniLog.Log(if_slot_node.GetSyncMember("Run"));
            Action a = (Action)Delegate.CreateDelegate(typeof(Action), if_slot_node, if_slot_node.GetType().GetMethod("Run"));
            Action b = (Action)Delegate.CreateDelegate(typeof(Action), if_slot_node, "Run");
            UniLog.Log(a);
            UniLog.Log(b);

            //fire_on_true_node.Pulse.Target = if_slot_node.Run;
            //fire_on_true_node.Pulse.Target = b;*/
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
                "Assembly Names :\n" +
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
            UniLog.Log("Getting Field Target through GetField and GetStructField");
            UniLog.Log(field.GetType().GetField("Target"));
            if (field != null)
            {
                //((FrooxEngine.LogiX.Input<int>)field).Target = int_val0;
                if (((ISyncRef)field).TrySet(int_val0) == false)
                {
                    UniLog.Log("Falling back on bad cast");
                    ((FrooxEngine.LogiX.Input<int>)field).Target = int_val0;
                }
                else
                {
                    UniLog.Log("Dekita !");
                }
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
                TestAddCodeWithOutputs(otherSlot);
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
