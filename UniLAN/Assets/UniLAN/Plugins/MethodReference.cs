using UnityEngine;
using System.Collections;

namespace UniLAN
{
    public class MethodReference
    {
        public System.Reflection.MethodInfo methodInfo;
        public Component component;

        public MethodReference(Component component, System.Reflection.MethodInfo methodInfo) {
            this.component = component;
            this.methodInfo = methodInfo;
        }

        public void Invoke(object[] parameters) {
            methodInfo.Invoke(component, parameters);
        }

    }
}