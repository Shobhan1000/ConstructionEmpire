using System;
using System.Globalization;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class DashboardElements
    {
        public static string Money(double value) => value.ToString("C0",CultureInfo.GetCultureInfo("en-GB"));
        public static Label Label(VisualElement parent,string text,string css=null,string name=null)
        { var label=new Label(text) { name=name }; if(css!=null) label.AddToClassList(css); parent.Add(label); return label; }
        public static VisualElement Box(VisualElement parent,string css,string name=null)
        { var box=new VisualElement {name=name}; box.AddToClassList(css); parent.Add(box); return box; }
        public static Button Button(VisualElement parent,string text,Action action,string css=null)
        { var button=new Button(action) {text=text}; if(css!=null) button.AddToClassList(css); parent.Add(button); return button; }
        public static void Metric(VisualElement parent,string id,string title,string value)
        { var box=Box(parent,"metric-card"); box.tooltip=DashboardTooltips.For(id); Label(box,title,"metric-label"); Label(box,value,"metric-value",id); }
        public static void Set(VisualElement root,string name,string text)
        { var label=root.Q<Label>(name); if(label!=null) label.text=text; }
        public static void Status(VisualElement element,string status)
        {
            foreach(string css in new[]{"healthy","warning","critical","info"}) element.RemoveFromClassList("status-"+css);
            element.AddToClassList("status-"+(status=="Complete" || status=="On track" || status=="Low Risk" ? "healthy" : status=="At risk" || status=="Medium Risk" ? "warning" : status=="Delayed" || status=="Over budget" || status=="High Risk" || status=="Critical Risk" ? "critical" : "info"));
        }
    }
}
