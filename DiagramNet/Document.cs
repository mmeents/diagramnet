﻿using System.Drawing.Drawing2D;
using System.Runtime.Serialization;
using DiagramNet.Elements;
using DiagramNet.Events;
using StaticExtensions;

namespace DiagramNet {
	/// <summary>
	/// This class control the elements collection and visualization.
	/// </summary>
	[Serializable]
	public class Document : IDeserializationCallback {
		//Draw properties
		private SmoothingMode smoothingMode = SmoothingMode.HighQuality;
		private PixelOffsetMode pixelOffsetMode = PixelOffsetMode.Default;
		private CompositingQuality compositingQuality = CompositingQuality.AssumeLinear;

		//Action
		private DesignerAction action = DesignerAction.Select;
		private ElementType elementType = ElementType.RectangleNode;
		private LinkType linkType = LinkType.RightAngle;

		// Element Collection
		internal ElementCollection elements = new();

		// Selections Collections
		private readonly ElementCollection selectedElements = new();
		private readonly ElementCollection selectedNodes = new();

		//Document Size
		private Point location = new(100, 100);		
		private Size windowSize = new(0, 0);

		//Zoom
		private float zoom = 1.0f;

		//Grig
		private Size gridSize = new(50, 50);

		//Events
		private bool canFireEvents = true;

		public Document() { }


		#region Add Methods
		public void AddElement(BaseElement el) {
			elements.Add(el);
			el.AppearanceChanged += new EventHandler(Element_AppearanceChanged);
			OnAppearancePropertyChanged(new EventArgs());
		}

		public void AddElements(ElementCollection els) {
			BaseElement?[] elementCollection = els.GetArray();
			if (elementCollection is not null) {
				BaseElement[] eleCollNoNull = elementCollection as BaseElement[];
			  AddElements(eleCollNoNull);
			}
		}

		public void AddElements(BaseElement[] els) {
			elements.EnabledCalc = false;
			foreach (BaseElement el in els) {
				this.AddElement(el);
			}
			elements.EnabledCalc = true;
		}

		internal static bool CanAddLink(ConnectorElement connStart, ConnectorElement connEnd) {
			return ((connStart != connEnd) && (connStart.ParentElement != connEnd.ParentElement));
		}

		public BaseLinkElement? AddLink(ConnectorElement connStart, ConnectorElement connEnd) {
			if (CanAddLink(connStart, connEnd)) {
				BaseLinkElement lnk;

				if (linkType == LinkType.Straight)
					lnk = new StraightLinkElement(connStart, connEnd);
				else // (linkType == LinkType.RightAngle)
					lnk = new RightAngleLinkElement(connStart, connEnd);

				elements.Add(lnk);
				lnk.AppearanceChanged += new EventHandler(Element_AppearanceChanged);
				OnAppearancePropertyChanged(new EventArgs());
				return lnk;
			} else {
				return null;
			}
		}
		#endregion

		#region Delete Methods

		public void DeleteElement(BaseElement? el) {
			if ((el != null) && el is not ConnectorElement) {
				//Delete link
				if (el is BaseLinkElement) {
					BaseLinkElement lnk = (BaseLinkElement)el;
					DeleteLink(lnk);
					return;
				}

        //Delete node
        if (el is NodeElement conn) {
          foreach (ConnectorElement elconn in conn.Connectors) {
            BaseLinkElement? lnk;
            for (int i = elconn.Links.Count - 1; i >= 0; i--) {
              lnk = (BaseLinkElement?)elconn.Links[i];
              DeleteLink(lnk);
            }
          }

          if (selectedNodes.Contains(el))
            selectedNodes.Remove(el);
        }

        if (SelectedElements.Contains(el))
					selectedElements.Remove(el);

				elements.Remove(el);

				OnAppearancePropertyChanged(new EventArgs());
			}
    }

    public void DeleteElement(Point p) {
			BaseElement? selectedElement = FindElement(p);
			DeleteElement(selectedElement);
		}

		public void DeleteSelectedElements() {
			selectedElements.EnabledCalc = false;
			selectedNodes.EnabledCalc = false;

			for (int i = selectedElements.Count - 1; i >= 0; i--) {
				DeleteElement(selectedElements[i]);
			}

			selectedElements.EnabledCalc = true;
			selectedNodes.EnabledCalc = true;
		}

		public void DeleteLink(BaseLinkElement? lnk) {
			if (lnk != null) {
				lnk.Connector1.RemoveLink(lnk);
				lnk.Connector2.RemoveLink(lnk);

				if (elements.Contains(lnk))
					elements.Remove(lnk);
				if (selectedElements.Contains(lnk))
					selectedElements.Remove(lnk);
				OnAppearancePropertyChanged(new EventArgs());
			}
		}
		#endregion

		#region Select Methods
		public void ClearSelection() {
			selectedElements.Clear();
			selectedNodes.Clear();
			OnElementSelection(this, new ElementSelectionEventArgs(selectedElements));
		}

		public void SelectElement(BaseElement el) {
			selectedElements.Add(el);
			if (el is NodeElement) {
				selectedNodes.Add(el);
			}
			if (canFireEvents)
				OnElementSelection(this, new ElementSelectionEventArgs(selectedElements));
		}

		public void SelectElements(BaseElement[] els) {
			selectedElements.EnabledCalc = false;
			selectedNodes.EnabledCalc = false;

			canFireEvents = false;

			try {
				foreach (BaseElement el in els) {
					SelectElement(el);
				}
			} finally {
				canFireEvents = true;
			}
			selectedElements.EnabledCalc = true;
			selectedNodes.EnabledCalc = true;

			OnElementSelection(this, new ElementSelectionEventArgs(selectedElements));
		}

		public void SelectElements(Rectangle selectionRectangle) {
			selectedElements.EnabledCalc = false;
			selectedNodes.EnabledCalc = false;

			// Add all "hitable" elements
			foreach (BaseElement element in elements) {
				if (element is IControllable controllable) {
					IController ctrl = controllable.GetController();
					if (ctrl.HitTest(selectionRectangle)) {
						if (element is not ConnectorElement)
							selectedElements.Add(element);

						if (element is NodeElement)
							selectedNodes.Add(element);
					}
				}
			}

			//if the seleciont isn't a expecific link, remove links
			// without 2 elements in selection
			if (selectedElements.Count > 1) {
				foreach (BaseElement el in elements) {
          if (el is not BaseLinkElement lnk) continue;

          if ((!selectedElements.Contains(lnk.Connector1.ParentElement)) ||
						(!selectedElements.Contains(lnk.Connector2.ParentElement))) {
						selectedElements.Remove(lnk);
					}
				}
			}

			selectedElements.EnabledCalc = true;
			selectedNodes.EnabledCalc = true;

			OnElementSelection(this, new ElementSelectionEventArgs(selectedElements));
		}

		public void SelectAllElements() {
			selectedElements.EnabledCalc = false;
			selectedNodes.EnabledCalc = false;

			foreach (BaseElement element in elements) {
				if (element is not ConnectorElement)
					selectedElements.Add(element);

				if (element is NodeElement)
					selectedNodes.Add(element);
			}

			selectedElements.EnabledCalc = true;
			selectedNodes.EnabledCalc = true;

		}

		public BaseElement? FindElement(Point point) {
			BaseElement? el;
			if ((elements != null) && (elements.Count > 0)) {
				// First, find elements
				for (int i = elements.Count - 1; i >= 0; i--) {
					el = elements[i];

					if (el is BaseLinkElement)
						continue;

          //Find element in a Connector array
          if (el is NodeElement nel) {
            foreach (ConnectorElement cel in nel.Connectors) {
              IController ctrl = ((IControllable)cel).GetController();
              if (ctrl.HitTest(point))
                return cel;
            }
          }

          //Find element in a Container Element
          if (el is IContainer container) {
						BaseElement? inner = FindInnerElement(container, point);
						if (inner != null)
							return inner;
					}

					//Find element by hit test
					if (el is IControllable controllable) {
						IController ctrl = controllable.GetController();
						if (ctrl.HitTest(point))
							return el;
					}
				}

				// Then, find links
				for (int i = elements.Count - 1; i >= 0; i--) {
					el = elements[i];

					if (el is not BaseLinkElement)
						continue;

					if (el is IControllable controllable) {
						IController ctrl = controllable.GetController();
						if (ctrl.HitTest(point))
							return el;
					}
				}
			}
			return null;
		}
		public BaseElement? FindElement(string name) {
			BaseElement? el;
			if ((elements != null) && (elements.Count > 0)) {
				// First, find elements
				for (int i = elements.Count - 1; i >= 0; i--) {
					el = elements[i];

					if (el is BaseLinkElement)
						continue;

          //Find element in a Connector array
          if (el is NodeElement nel) {
            if (el.Name == name) {
              return el;
            }
            foreach (ConnectorElement cel in nel.Connectors) {
              if (cel.Name == name) {
                return cel;
              }
            }
          }

          //Find element in a Container Element
          if (el is IContainer container) {
						BaseElement? inner = FindInnerElement(container, name);
						if (inner != null)
							return inner;
					}


				}

				// Then, find links
				for (int i = elements.Count - 1; i >= 0; i--) {
					el = elements[i];

					if (el is not BaseLinkElement)
						continue;

					if (el.Name == name) {
						return el;
					}
				}
			}
			return null;
		}
		private BaseElement? FindInnerElement(IContainer parent, string name) {
			foreach (BaseElement el in parent.Elements) {
				if (el is IContainer container) {
					BaseElement? retEl = FindInnerElement(container, name);
					if (retEl != null)
						return retEl;
				}

        if (el is IControllable) {
          if (el.Name == name) {
						return el;
					}
				}
			}
			return null;
		}
		private BaseElement? FindInnerElement(IContainer parent, Point hitPos) {
			foreach (BaseElement el in parent.Elements) {
				if (el is IContainer container) {
					BaseElement? retEl = FindInnerElement(container, hitPos);
					if (retEl != null)
						return retEl;
				}

				if (el is IControllable controllable) {
					IController ctrl = controllable.GetController();

					if (ctrl.HitTest(hitPos))
						return el;
				}
			}
			return null;
		}
		#endregion

		#region Position Methods
		public void MoveUpElement(BaseElement el) {
			int i = elements.IndexOf(el);
			if (i != elements.Count - 1) {
				elements.ChangeIndex(i, i + 1);
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		public void MoveDownElement(BaseElement el) {
			int i = elements.IndexOf(el);
			if (i != 0) {
				elements.ChangeIndex(i, i - 1);
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		public void BringToFrontElement(BaseElement el) {
			int i = elements.IndexOf(el);
			for (int x = i + 1; x <= elements.Count - 1; x++) {
				elements.ChangeIndex(i, x);
				i = x;
			}
			OnAppearancePropertyChanged(new EventArgs());
		}

		public void SendToBackElement(BaseElement el) {
			int i = elements.IndexOf(el);
			for (int x = i - 1; x >= 0; x--) {
				elements.ChangeIndex(i, x);
				i = x;
			}
			OnAppearancePropertyChanged(new EventArgs());
		}
		#endregion

		internal void CalcWindow(bool forceCalc) {
			elements.CalcWindow(forceCalc);
			selectedElements.CalcWindow(forceCalc);
			selectedNodes.CalcWindow(forceCalc);
		}

		#region Properties
		public ElementCollection Elements {
			get {
				return elements;
			}
		}

		public ElementCollection SelectedElements {
			get {
				return selectedElements;
			}
		}

		public ElementCollection SelectedNodes {
			get {
				return selectedNodes;
			}
		}

		public Point Location {
			get {
				return elements.WindowLocation;
			}
		}

		public Size Size {
			get {
				return elements.WindowSize;
			}
		}

		internal Size WindowSize {
			set {
				windowSize = value;
			}
		}

		public SmoothingMode SmoothingMode {
			get {
				return smoothingMode;
			}
			set {
				smoothingMode = value;
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		public PixelOffsetMode PixelOffsetMode {
			get {
				return pixelOffsetMode;
			}
			set {
				pixelOffsetMode = value;
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		public CompositingQuality CompositingQuality {
			get {
				return compositingQuality;
			}
			set {
				compositingQuality = value;
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		public DesignerAction Action {
			get {
				return action;
			}
			set {
				action = value;
				OnPropertyChanged(new EventArgs());
			}
		}

		public float Zoom {
			get {
				return zoom;
			}
			set {
				zoom = value;
				OnPropertyChanged(new EventArgs());
			}
		}

		public ElementType ElementType {
			get {
				return elementType;
			}
			set {
				elementType = value;
				OnPropertyChanged(new EventArgs());
			}
		}

		public LinkType LinkType {
			get {
				return linkType;
			}
			set {
				linkType = value;
				OnPropertyChanged(new EventArgs());
			}
		}

		public Size GridSize {
			get {
				return gridSize;
			}
			set {
				gridSize = value;
				OnAppearancePropertyChanged(new EventArgs());
			}
		}

		#endregion

		#region Draw Methods
		internal void DrawElements(Graphics g, Rectangle clippingRegion) {
			//Draw Links first
			for (int i = 0; i <= elements.Count - 1; i++) {
				BaseElement? el = elements[i];
				if ((el is BaseLinkElement) && (NeedDrawElement(el, clippingRegion)))
					el.Draw(g);

				if (el is ILabelElement element)
					element.Label.Draw(g);
			}

			//Draw the other elements
			for (int i = 0; i <= elements.Count - 1; i++) {
				BaseElement? el = elements[i];

				if (el is not BaseLinkElement && NeedDrawElement(el, clippingRegion)) {
          if (el is NodeElement n) {
            n.Draw(g, (action == DesignerAction.Connect));
          } else {
            el?.Draw(g);
          }

          if (el is ILabelElement element)
						element.Label.Draw(g);
				}
			}
		}

		private static bool NeedDrawElement(BaseElement? el, Rectangle clippingRegion) {
			if (el is null || !el.Visible) return false;

			Rectangle elRectangle = el.GetUnsignedRectangle();
			elRectangle.Inflate(5, 5);
			return clippingRegion.IntersectsWith(elRectangle);
		}

		internal void DrawSelections(Graphics g) {
			for (int i = selectedElements.Count - 1; i >= 0; i--) {
				if (selectedElements[i] is IControllable controllable) {
					IController ctrl = controllable.GetController();
					ctrl.DrawSelection(g);

          if (selectedElements[i] is BaseLinkElement link) {
            ctrl = ((IControllable)link.Connector1).GetController();
            ctrl.DrawSelection(g);

            ctrl = ((IControllable)link.Connector2).GetController();
            ctrl.DrawSelection(g);
          }
        }
			}
		}

		internal void DrawGrid(Graphics g) {
			Pen p = new(new HatchBrush(HatchStyle.LargeGrid | HatchStyle.Percent90, Color.LightGray, Color.Transparent), 1);
			int maxX = location.X + this.Size.Width;
			int maxY = location.Y + this.Size.Height;

			if (windowSize.Width / zoom > maxX)
				maxX = (int)(windowSize.Width / zoom);

			if (windowSize.Height / zoom > maxY)
				maxY = (int)(windowSize.Height / zoom);

			for (int i = 0; i < maxX; i += gridSize.Width) {
				g.DrawLine(p, i, 0, i, maxY);
			}

			for (int i = 0; i < maxY; i += gridSize.Height) {
				g.DrawLine(p, 0, i, maxX, i);
			}

			p.Dispose();
		}
		#endregion

		#region Events Raising

		// Property Changed
		[field: NonSerialized]
		public event EventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged(EventArgs e) {
      PropertyChanged?.Invoke(this, e);
    }

		// Appearance Property Changed
		[field: NonSerialized]
		public event EventHandler? AppearancePropertyChanged;

		protected virtual void OnAppearancePropertyChanged(EventArgs e) {
			OnPropertyChanged(e);

      AppearancePropertyChanged?.Invoke(this, e);

    }

		// Element Property Changed
		[field: NonSerialized]
		public event EventHandler? ElementPropertyChanged;

		protected virtual void OnElementPropertyChanged(object? sender, EventArgs e) {
      ElementPropertyChanged?.Invoke(sender, e);
    }

		// Element Selection
		public delegate void ElementSelectionEventHandler(object? sender, ElementSelectionEventArgs e);

		[field: NonSerialized]
		public event ElementSelectionEventHandler? ElementSelection;

		protected virtual void OnElementSelection(object? sender, ElementSelectionEventArgs e) {
      ElementSelection?.Invoke(sender, e);
    }


		#endregion

		#region Events Handling
		private void RecreateEventsHandlers() {
			foreach (BaseElement el in elements)
				el.AppearanceChanged += new EventHandler(Element_AppearanceChanged);
		}

		//[SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
		private void Element_AppearanceChanged(object? sender, EventArgs e) {
			OnElementPropertyChanged(sender, e);
		}
		#endregion

		public void Save(string fileName) {
			if (elements is not null) {
				if (File.Exists(fileName)) {
					File.Delete(fileName);
				}
				using StreamWriter w = File.CreateText(fileName);
				string? elementsAsJson = elements.AsJsonString();
				if (elementsAsJson is not null) {
					w.Write(elementsAsJson);
				}
				
			}
		}

		public void Open(string fileName) {
			string sRawFileTxt;
			if (File.Exists(fileName)) {
				sRawFileTxt = File.ReadAllText(fileName);
				if (sRawFileTxt is not null) {
					ElementCollection? document1 = (ElementCollection?)sRawFileTxt.AsFromJsonString();
					if (document1 is not null) {
						elements = document1;
						RecreateEventsHandlers();
					}
				}
			}
		}

		#region IDeserializationCallback Members
		void IDeserializationCallback.OnDeserialization(object? sender) {
			RecreateEventsHandlers();
		}
		#endregion
	}
}
