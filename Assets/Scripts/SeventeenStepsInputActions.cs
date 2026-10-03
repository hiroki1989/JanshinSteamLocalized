using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Each dynamic scene owns its input asset instead of reusing a disposed package default.
public sealed class SeventeenStepsInputActions : MonoBehaviour {
 DefaultInputActions actions;
 public void Configure(InputSystemUIInputModule module){
  actions=new DefaultInputActions();module.actionsAsset=actions.asset;
  module.cancel=InputActionReference.Create(actions.UI.Cancel);module.submit=InputActionReference.Create(actions.UI.Submit);
  module.move=InputActionReference.Create(actions.UI.Navigate);module.leftClick=InputActionReference.Create(actions.UI.Click);
  module.rightClick=InputActionReference.Create(actions.UI.RightClick);module.middleClick=InputActionReference.Create(actions.UI.MiddleClick);
  module.point=InputActionReference.Create(actions.UI.Point);module.scrollWheel=InputActionReference.Create(actions.UI.ScrollWheel);
  module.trackedDeviceOrientation=InputActionReference.Create(actions.UI.TrackedDeviceOrientation);
  module.trackedDevicePosition=InputActionReference.Create(actions.UI.TrackedDevicePosition);
 }
 void OnDestroy(){actions?.Dispose();}
}
