using UnityEngine;
using UnityEngine.EventSystems;

namespace RefractionRoom {
 // Full-screen pad behind the HUD for touch (and mouse in touch mode): drag to look / sweep, tap to use what is under the finger.
 public class ClinicTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler {
  public ClinicWorld world;
  bool dragged;
  public void OnPointerDown(PointerEventData e){dragged=false;}
  public void OnDrag(PointerEventData e){if(e.delta.sqrMagnitude>4)dragged=true;world.TouchDrag(e.delta);}
  public void OnPointerUp(PointerEventData e){world.TouchRelease();}
  public void OnPointerClick(PointerEventData e){if(!dragged)world.TouchTap(e.position);}
 }
 // Virtual joystick for walking.
 public class ClinicStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
  public RectTransform knob;public float radius=90;public Vector2 value;
  RectTransform rt;
  void Awake(){rt=(RectTransform)transform;}
  public void OnPointerDown(PointerEventData e){OnDrag(e);}
  public void OnDrag(PointerEventData e){Vector2 p;RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,e.position,e.pressEventCamera,out p);p=Vector2.ClampMagnitude(p,radius);knob.anchoredPosition=p;value=p/radius;}
  public void OnPointerUp(PointerEventData e){value=Vector2.zero;knob.anchoredPosition=Vector2.zero;}
 }
}
