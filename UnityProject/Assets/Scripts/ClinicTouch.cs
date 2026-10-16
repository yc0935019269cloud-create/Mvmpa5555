using UnityEngine;
using UnityEngine.EventSystems;

namespace RefractionRoom {
 // Full-screen pad behind the HUD for touch (and mouse in touch mode): drag to look / sweep, tap to use what is under the finger.
 // A finger that lands on an adjustable knob turns the knob instead (no tap-first step).
 public class ClinicTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler {
  public ClinicWorld world;
  bool dragged,knob;int finger=int.MinValue;Vector2 last,start;
  // Only the finger that started on the pad turns the view. Deltas are computed from that finger's own
  // last position (the browser can mix up touch indices when a second finger is on the joystick), and
  // a single event may not move more than 60 px, so a swapped finger can never make the view jump.
  public void OnPointerDown(PointerEventData e){if(finger!=int.MinValue&&finger!=e.pointerId)return;finger=e.pointerId;last=start=e.position;dragged=false;knob=world.KnobDown(e.position,e.pointerId);}
  public void OnDrag(PointerEventData e){if(e.pointerId!=finger)return;
   if(knob){if((e.position-start).sqrMagnitude>100)dragged=true;world.KnobMove(e.position);return;}
   if(world.touchOwnsInput){if((e.position-last).sqrMagnitude>100)dragged=true;return;}Vector2 d=e.position-last;last=e.position;if(d.sqrMagnitude>140*140)return;d=Vector2.ClampMagnitude(d,60);if(d.sqrMagnitude>4)dragged=true;world.TouchDrag(d);}
  public void OnPointerUp(PointerEventData e){if(e.pointerId!=finger)return;finger=int.MinValue;if(knob){knob=false;world.KnobUp();return;}if(!world.touchOwnsInput)world.TouchRelease();}
  public void OnPointerClick(PointerEventData e){if(!dragged&&!world.TouchLookMoved)world.TouchTap(e.position);}
  void OnDisable(){if(knob){knob=false;world.KnobUp();}finger=int.MinValue;}
 }
 // The retinoscope eyepiece panel (top right) sweeps the streak too: drag anywhere on it (finger or mouse).
 public class ClinicSweepPad : MonoBehaviour, IDragHandler, IEndDragHandler {
  public ClinicWorld world;
  public void OnDrag(PointerEventData e){world.TouchDrag(Vector2.ClampMagnitude(e.delta,60));}
  public void OnEndDrag(PointerEventData e){world.TouchRelease();}
 }
 // Virtual joystick for walking.
 public class ClinicStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
  public RectTransform knob;public float radius=90;public Vector2 value;
  RectTransform rt;
  void Awake(){rt=(RectTransform)transform;}
  int finger=int.MinValue;
  bool Touching{get{return Input.touchCount>0;}}
  public void OnPointerDown(PointerEventData e){if(Touching)return;if(finger!=int.MinValue&&finger!=e.pointerId)return;finger=e.pointerId;Move(e);}
  public void OnDrag(PointerEventData e){if(Touching)return;if(e.pointerId==finger)Move(e);}
  public bool Contains(Vector2 screen){Vector2 p;Canvas c=GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,screen,null,out p);return p.magnitude<rt.rect.width*.6f;}
  public void SetScreen(Vector2 screen){Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,screen,null,out p);if(p.magnitude>radius*2.4f){SetValue(Vector2.zero);return;}SetValue(Vector2.ClampMagnitude(p,radius)/radius);}
  public void SetValue(Vector2 v){value=v;if(knob)knob.anchoredPosition=v*radius;}
  void Move(PointerEventData e){Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,e.position,e.pressEventCamera,out p);if(p.magnitude>radius*2.2f)return;p=Vector2.ClampMagnitude(p,radius);knob.anchoredPosition=p;value=p/radius;}
  public void OnPointerUp(PointerEventData e){if(Touching&&finger==int.MinValue)return;if(e.pointerId!=finger)return;finger=int.MinValue;value=Vector2.zero;knob.anchoredPosition=Vector2.zero;}
  void OnDisable(){finger=int.MinValue;value=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
 }
}
