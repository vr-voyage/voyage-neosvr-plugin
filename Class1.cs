using System;
using BaseX;
using FrooxEngine;

namespace NeosQuickSetupTools
{
    [Category("MyHamsterIsRich")]
    public class Class1 : Component
    {

        public readonly RelayRef<IField<float>> blah;
        public RelayRef<IField<float>> bloh;
        public readonly FieldDrive<Slot> fieldSlotRO;
        public FieldDrive<bool> a;
        public readonly bool b;
        public readonly Sync<bool> c;
        //public readonly Sync<Slot> syncSlotRO;
        //public readonly RelayRef<IField<Slot>> relaySlotRO;
        //public readonly Sync<SkinnedMeshRenderer> syncRenderer;
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

        protected override void OnChanges()
        {
            base.OnChanges();
            UniLog.Log("-----------------");
            UniLog.Log("Changed triggered");

            

            //if (body != null && body.IsValid)
            if (skinRef             != null 
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
            }
            /*if (slot != null && slot.Target.Value != null)
            {
                UniLog.Log("Slot target value");
                UniLog.Log(slot.Target.Value);

                Slot bodySlot = slot.Target.Value;
                SkinnedMeshRenderer skin = bodySlot.GetComponent<SkinnedMeshRenderer>();
                IField<float> blendShapeField = skin.GetBlendShape("vrc.v_aa");
                 blendShapeField.DriveFromVariable("vrc.v_aa");
            }*/
        }

    }
}
