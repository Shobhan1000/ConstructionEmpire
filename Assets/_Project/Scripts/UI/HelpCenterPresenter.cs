using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Presentation;
using ConstructionEmpire.Data.Time;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal sealed class HelpCenterPresenter:IDisposable
    {
        private readonly MainDashboardController owner;
        private readonly VisualElement root,overlay,strip;
        private readonly Label context,title,category,body,resultCount;
        private readonly ScrollView results,article;
        private readonly TextField search;
        private readonly Dictionary<VisualElement,bool> disabled=new Dictionary<VisualElement,bool>();
        private HelpTopic selected=HelpCatalog.Get("start");
        private VisualElement lastTarget;
        private bool opened;
        public HelpCenterPresenter(MainDashboardController owner,VisualElement root)
        {
            this.owner=owner;this.root=root;root.focusable=true;
            DashboardElements.Button(root.Q("time-controls").parent,"? Help",()=>Open(selected.Id),"help-launch").tooltip="Search the handbook or press F1 for context help.";
            strip=new VisualElement {name="context-help"};strip.AddToClassList("help-context");
            context=new Label("Hover an item for a quick explanation. F1 opens its help topic.");context.AddToClassList("help-context-text");strip.Add(context);
            strip.Add(new Button(()=>Open(selected.Id)){text="Read more  /  F1"});
            var feedback=root.Q("feedback");feedback.parent.Insert(feedback.parent.IndexOf(feedback),strip);
            overlay=new VisualElement {name="help-center"};overlay.AddToClassList("help-overlay");root.Add(overlay);overlay.style.display=DisplayStyle.None;
            var panel=DashboardElements.Box(overlay,"help-panel");
            var top=DashboardElements.Box(panel,"help-toolbar");DashboardElements.Label(top,"FIELD GUIDE","help-eyebrow");
            search=new TextField("Search help") {name="help-search"};search.tooltip="Search every article by title, concept or action.";search.AddToClassList("help-search");top.Add(search);
            top.Add(new Button(Close){text="Close  /  Esc"});
            var columns=DashboardElements.Box(panel,"help-columns");var sidebar=DashboardElements.Box(columns,"help-sidebar");
            resultCount=DashboardElements.Label(sidebar,"", "help-result-count");results=new ScrollView();sidebar.Add(results);
            article=new ScrollView();article.AddToClassList("help-article");columns.Add(article);
            category=DashboardElements.Label(article,"","help-eyebrow");title=DashboardElements.Label(article,"","help-title");body=DashboardElements.Label(article,"","help-body");
            search.RegisterValueChangedCallback(e=>RenderResults(e.newValue));RenderResults("");
            root.RegisterCallback<PointerOverEvent>(OnPointer);root.RegisterCallback<PointerUpEvent>(OnPointerUp);root.RegisterCallback<FocusInEvent>(OnFocus);root.RegisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);
        }
        private void OnPointer(PointerOverEvent e){Explain(e.target as VisualElement);EnsureKeyboardTarget();}
        private void OnPointerUp(PointerUpEvent e)=>EnsureKeyboardTarget();
        private void EnsureKeyboardTarget(){if(root.focusController?.focusedElement==null)root.Focus();}
        private void OnFocus(FocusInEvent e){if(!ReferenceEquals(e.target,root))Explain(e.target as VisualElement);}
        private void Explain(VisualElement target)
        {
            if(target==null || opened || overlay.Contains(target) || strip.Contains(target) || lastTarget==target)return;
            lastTarget=target;string hint=null;string words="";
            for(var node=target;node!=null && node!=root;node=node.parent)
            {
                if(node is TextElement text && !string.IsNullOrWhiteSpace(text.text))words+=" "+text.text;
                words+=" "+node.name;
                if(hint==null && !string.IsNullOrWhiteSpace(node.tooltip) && !node.tooltip.EndsWith("Press F1 or Read more for full help.",StringComparison.Ordinal))hint=node.tooltip;
                if(node.ClassListContains("metric-card")){words=node.Q<Label>(className:"metric-label")?.text??words;break;}
                if(node.ClassListContains("form-field")){words+=" "+node.Q<Label>(className:"unity-base-field__label")?.text;break;}
                if(node is Button || node.ClassListContains("table-row"))break;
            }
            selected=HelpCatalog.ForControl(words,hint,owner.CurrentScreen);
            context.text=selected.Title+" — "+(hint??selected.Summary);
            context.tooltip=hint??selected.Summary;
            if(string.IsNullOrWhiteSpace(target.tooltip))target.tooltip=selected.Summary+" Press F1 or Read more for full help.";
        }
        private void OnKey(KeyDownEvent e)
        {if(e.keyCode==KeyCode.F1){Open(selected.Id);e.StopPropagation();}else if(e.keyCode==KeyCode.Escape && opened){Close();e.StopPropagation();}}
        public void Open(string id="start")
        {
            owner.Bootstrap.GameTime.Speed=SimulationSpeed.Pause;
            if(!opened){disabled.Clear();foreach(var child in root.Children().Where(x=>x!=overlay)){disabled[child]=child.enabledSelf;child.SetEnabled(false);}}
            opened=true;overlay.BringToFront();overlay.SetEnabled(true);overlay.style.display=DisplayStyle.Flex;search.SetValueWithoutNotify("");RenderResults("");Show(HelpCatalog.Get(id));search.Focus();
        }
        private void RenderResults(string query)
        {
            results.Clear();var found=HelpCatalog.Search(query);resultCount.text=found.Count+" articles · search all topics";
            foreach(var topic in found){var entry=new Button(()=>Show(topic)){text=topic.Category+"  /  "+topic.Title};entry.AddToClassList("help-result");results.Add(entry);}
            if(found.Count==0)DashboardElements.Label(results,"No match. Try cash, phase, material, crew, save or risk.","help-body");
        }
        private void Show(HelpTopic topic){selected=topic;category.text=topic.Category.ToUpperInvariant();title.text=topic.Title;body.text=topic.Summary+"\n\n"+topic.Body;article.scrollOffset=Vector2.zero;}
        public void Close(){if(!opened)return;opened=false;overlay.style.display=DisplayStyle.None;foreach(var pair in disabled)pair.Key.SetEnabled(pair.Value);disabled.Clear();}
        public void Dispose(){Close();root.UnregisterCallback<PointerOverEvent>(OnPointer);root.UnregisterCallback<PointerUpEvent>(OnPointerUp);root.UnregisterCallback<FocusInEvent>(OnFocus);root.UnregisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);strip.RemoveFromHierarchy();overlay.RemoveFromHierarchy();}
    }
}
