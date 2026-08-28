using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AgroAgents.Presentation.Editor
{
    /// <summary>
    /// Draws a type-picker dropdown for any [SerializeReference] field whose
    /// declared type is an interface. Attach via [SerializeReferenceDropdown] attribute
    /// or rely on the property drawer below which targets ISimulationConnector.
    /// </summary>
    [CustomPropertyDrawer(typeof(SimulationPort.ISimulationConnector), true)]
    public sealed class SerializeReferenceDropdown : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Find all concrete types implementing the interface across all assemblies.
            Type interfaceType = fieldInfo.FieldType;
            List<Type> implementations = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();

            // Current selection
            string currentTypeName = property.managedReferenceFullTypename;
            int currentIndex = 0;

            var displayNames = new List<string> { "(None)" };
            for (int i = 0; i < implementations.Count; i++)
            {
                displayNames.Add(implementations[i].Name);
                string fullName = $"{implementations[i].Assembly.GetName().Name} {implementations[i].FullName}";
                if (fullName == currentTypeName)
                {
                    currentIndex = i + 1;
                }
            }

            // Draw dropdown
            Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, displayNames.ToArray());

            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    Type selectedType = implementations[newIndex - 1];
                    property.managedReferenceValue = Activator.CreateInstance(selectedType);
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
