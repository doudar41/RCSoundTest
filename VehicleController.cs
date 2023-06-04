using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using Rewired;
using System.Collections;


namespace MMG.RCCAR
{
	public enum SpeedUnit
	{
		mph,
		kmh
	}
	
	public enum CarState
	{
		NOT_STARTED,
		RACE,
		FRIZED,
		RECOVERY,
	}
	
	[RequireComponent(typeof(Rigidbody))]
	public class VehicleController : MonoBehaviour
	{
		[Header("Input Control")]
		public int playerId = 0;
		public CarState CarState;
		
		[Header("Speed")]
		public float currentSpeed;
		public float velocitySpeed;
		[SerializeField] private SpeedUnit speedUnit;
		[SerializeField] private float maxSpeed;

		public Text textEngine, gearText;
		
		[Header("Wheels Setup")]
		public List<CarAxle> Axles;

		#region public members
		[Space]
		public ControlType ControlType; 
		[Space]
		public CarProperties Properties; 
		[Space]
		public Transform groundCheck;
		public Transform fricAt;
		public Transform CentreOfMass;
		[Space]
		[Header("AI")]
		public Transform TargetTransform;
		[Space]
		public Sensors sensorScript;
		[Space]
		[Header("VehicleBalance")] 
		public bool VehicleBalanceEnable;
		[Space]
		[Header("Camera Controller")]
		public List<Transform> CameraControllers;
		[Space]
		[Header("Forces")] 
		public bool EnableCustomGravity = true;
		public float CustomGravity = 30f;
		[Tooltip("Air Force for pitch/tangage")]
		public float airForce = 1f;
		[Space]
		[Header("Skin Preset")] 
		public CarSkinPreset skin;

		[Space] [Header("AudioSource")]
		//public AudioSource audioEngine;
		public AudioSource audioTire;
		[SerializeField]
		private AnimationCurve rpmRise;
		[SerializeField]
		private AnimationCurve speedCheckCurve;
		[SerializeField]
		private AnimationCurve[] gearChangeRPMCurves;
		[SerializeField]
		private StudioEventEmitter engineFMOD;
		[SerializeField]
		private StudioEventEmitter brakesFMOD;		
		[SerializeField]
		private StudioEventEmitter tiresFMOD;
		[SerializeField]
		private StudioEventEmitter turnFMOD;




		[SerializeField]
		private float rpmCoefficient;

		public int currentGear;
		float speedTemp;
		float RPM;


		#endregion public members


		#region public properties

		public Vector3 CarVelocity
		{
			get => carVelocity;
			set => carVelocity = value;
		}


		public bool IsFirstCheckpointPassed { get; set; }


		// пока не активно не передаем управиление (машинка не едет)
		public bool IsActive => _isActive;
		
		public bool IsGrounded
		{
			get
			{
				bool _isGrounded = false;

				foreach (var ax in Axles)
				{
					if (ax.LeftSuspension.IsGrounded && ax.RightSuspension.IsGrounded)
					{
						_isGrounded = true;
					}
				}

				return _isGrounded;
			}
		}
		
		public float SkidEnable => Properties.SkidEnable;
		
		public float AccelerationForce => accelForce;


		public float SpeedCurve => Properties.speedCurve.Evaluate(Mathf.Abs(carVelocity.z) / 100);

		#endregion public properties
		
		#region private members
		
		private Rigidbody _rb;
		private bool _isActive;
		
		private Player player;
		private float moveHorizontal;
		private float moveVertical;
		private float brakePlayerInput;
		private float resetPlayerInput;
		
		private float turnInput;
		private float turnAngle;
		private float turnSpeed;
		private float turnValue;

		private float speedInput;
		private float accelForce;
		private float speedValue;

		private float fakeRPM;
		private bool fakeBoost = false;
		private float pressAccelerateTime;
		private bool tempTurnInput;

		private float brakeInput;
		private float brakeForce;
		private float brakeValue;
		private float frictionForce;
		private float fricValue;
		
		private float slerpTime = 0.1000f;
		
		private Vector3 carVelocity; 
		
		//private Vector3 normalDir;
		private bool _isInverse;
		private int _idxActiveCamera;
		private int _idxActiveWheels;
		
		// Custom gravity
		private Vector3 _customGForce; 
		
		//Ai stuff
		private float _desiredTurning;
		private float _turnAI = .1f;
		private float _speedAI = .1f;
		private float _brakeAI = 0f;
		private Vector3 targetPosition;
		private bool _stopVehicle;
		private ReverseWhenStuck _reverseWhenStuck;


		FMOD.Studio.Bus MasterBus;
		FMOD.Studio.Bus EngineBus;
		FMOD.Studio.Bus BrakesBus;	
		FMOD.Studio.Bus TiresBus;


		#endregion private members

		private bool _isNeedBooster;
		public void SetNeedBooster(bool value) { _isNeedBooster = value; }	
		public void Initializate()
		{
			player = ReInput.players.GetPlayer(playerId);
			//var cMaps = player.controllers.maps.GetMaps(ControllerType.Keyboard, 0);
			//player.controllers.maps.SetMapsEnabled(true, ControllerType.Keyboard, "");
			_rb = GetComponent<Rigidbody>();

			CarState = CarState.NOT_STARTED;
			
			UpdateVehicleParams();

			_idxActiveCamera = 0;
			_idxActiveWheels = 0;

			var idx = 0;
			
			foreach (var cam in CameraControllers)
			{
				cam?.gameObject.SetActive(_idxActiveCamera == idx);
				idx++;
			}
			
			InitializateWheels();

/*			audioEngine.clip = Properties.engineSound;
			audioEngine.mute = true;
			audioEngine.Play();*/
/*			audioTire.clip = Properties.skidSound;
			audioTire.mute = true;
			audioTire.Play();*/
		}
		
		public void StartVehicle()
		{
			_stopVehicle = false;
			CarState = CarState.RACE;
			//audioEngine.mute = false;
			
			_reverseWhenStuck = GetComponent<ReverseWhenStuck>();
			if (_reverseWhenStuck == null)
			{
				_reverseWhenStuck = gameObject.AddComponent<ReverseWhenStuck>();
			}

			if (ControlType != ControlType.AI)
			{
				_reverseWhenStuck.enabled = false;
			}
			else
			{
				_reverseWhenStuck.enabled = true;	
			}
			
		}

		public void StopVehicle()
		{
			_stopVehicle = true;
			CarState = CarState.NOT_STARTED;
			_reverseWhenStuck = GetComponent<ReverseWhenStuck>();
			if (_reverseWhenStuck)
			{
				_reverseWhenStuck.enabled = false;
			}
		}
		
		public void SwitchControlType(ControlType controlType)
		{ 
			//if (ControlType == controlType) return;

			ControlType = controlType;
			_reverseWhenStuck = GetComponent<ReverseWhenStuck>();
			if (_reverseWhenStuck == null)
			{
				_reverseWhenStuck = gameObject.AddComponent<ReverseWhenStuck>();
			}

			if (ControlType != ControlType.AI)
			{
				_reverseWhenStuck.enabled = false;
			}
			else
			{
				_reverseWhenStuck.enabled = true;	
			}
			
			UpdateVehicleParams();
		}

		private void Awake()
		{
			Initializate();

			if (ControlType == ControlType.NONE) return;
			StartVehicle();
			
			_isActive = true;


		}

        private void Start()
        {
			MasterBus = RuntimeManager.GetBus("bus:/Master");
			EngineBus = RuntimeManager.GetBus("bus:/Master/SFX/EngineMixer");
			BrakesBus = RuntimeManager.GetBus("bus:/Master/SFX/BrakesMixer");
			TiresBus = RuntimeManager.GetBus("bus:/Master/SFX/TiresMixer");
		}
        private void UpdateVehicleParams()
		{
			_rb.centerOfMass = CentreOfMass.localPosition;
			_rb.drag = Properties.dragAmount;
			_rb.useGravity = !EnableCustomGravity;

			_customGForce = Vector3.up * CustomGravity * _rb.mass;
			
			if (ControlType == ControlType.AI)
			{
				turnSpeed = Properties.turnSpeed * Properties.turnSpeed_AI_Mod;
			}
			else
			{
				turnSpeed = Properties.turnSpeed;
			}
			
			//turnSpeed = Properties.turnSpeed;
			turnAngle = Properties.turnAngle;
			accelForce = Properties.accelSpeed;
			brakeForce = Properties.brake;
			frictionForce = Properties.friction;
			maxSpeed = Properties.maxSpeed;
		}
		
		
		public void InitializateWheels()
		{
			
			// reset!
			_rb.transform.position.SetY(1f);
			
			//_idxActiveWheels
			//Tire mesh rotate
			//var wheelsProperty = WheelsProperties[_idxActiveWheels];
			
			foreach (var ax in Axles)
			{
				//ax.WheelsProperties.gumFriction
				
				// remove
				var axParent = ax.LeftSuspension.wheelBody;
				CleanChildren(axParent);
				
				var wheelsProperty = ax.WheelsProperties;
				var left = Instantiate(wheelsProperty.leftWheel, Vector3.zero, Quaternion.identity, axParent);
				left.transform.localPosition = Vector3.zero;
				left.transform.localRotation = Quaternion.identity;
				ax.LeftWheel = left.GetComponent<WheelParts>();
				ax.LeftWheel.skids.SetSkidWidth(ax.LeftWheel.skidWidth);

				// пройтись по всем запчастям 
				//ax.LeftWheel.wheel_parts
				
				// этот материал, надо применить
				//ax.LeftWheel.wheel_parts[0].material
				
				// ко всем объектам в parts (foreach)
				//ax.LeftWheel.wheel_parts[0].parts
				
				// ищем MeshRenderer, обновляем материал
				//ax.LeftWheel.wheel_parts[i].parts[j].GetComponent<MeshRenderer>().material;

				
				
				axParent = ax.RightSuspension.wheelBody;
				CleanChildren(axParent);
				
				var right = Instantiate(wheelsProperty.rightWheel, Vector3.zero, Quaternion.identity, axParent);
				right.transform.localPosition = Vector3.zero;
				right.transform.localRotation = Quaternion.identity;
				ax.RightWheel = right.GetComponent<WheelParts>();
				ax.RightWheel.skids.SetSkidWidth(ax.RightWheel.skidWidth);
				
				
				ax.LeftSuspension.suspensionDistanceConstant = Properties.SuspensionDistance;
				ax.LeftSuspension.springConstant = Properties.suspensionForce;
				ax.LeftSuspension.damperConstant = Properties.suspensionDamper;
				ax.LeftSuspension.wheelRadius = wheelsProperty.wheelRadius;
				
				ax.RightSuspension.suspensionDistanceConstant = Properties.SuspensionDistance;
				ax.RightSuspension.springConstant = Properties.suspensionForce;
				ax.RightSuspension.damperConstant = Properties.suspensionDamper;
				ax.RightSuspension.wheelRadius = wheelsProperty.wheelRadius;

			}

		}
		
		private void CleanChildren(Transform mTargetObject) 
		{
			int nbChildren = mTargetObject.childCount;

			for (int i = nbChildren - 1; i >= 0; i--) 
			{
				DestroyImmediate(mTargetObject.GetChild(i).gameObject);
			}
		}

		private void Update()
		{
			switch (ControlType)
			{
				case ControlType.NONE:
					return;
				case ControlType.AI:
					UpdateAI();
					UpdateAIControl();
					break;
				case ControlType.PLAYER:
					UpdateInputControl();
					break;
			}
			
			TireVisuals();
			AudioControl();
			AudioForFakeGearChange();
		}


		private void FixedUpdate()
		{
			if (ControlType == ControlType.NONE)
			{
				if (EnableCustomGravity) _rb.AddForce(_customGForce);
				return;
			}
			
			float fdt = Time.fixedDeltaTime * 1000;

			if (resetPlayerInput > 0  && ControlType == ControlType.PLAYER)
			{
				Recovery();
			}
			
			if (ControlType == ControlType.AI)
			{
				ResetAuto();
			}
			
			carVelocity = transform.InverseTransformDirection(_rb.velocity);
			//speedVector = _rb.velocity;
			//velocityX = carVelocity.x;

			if (IsGrounded)
			{
				AccelerationLogic();
				TurningLogic();
				FrictionLogic();
				BrakeLogic();
				//for drift behaviour
				_rb.angularDrag = Properties.dragAmount * Properties.driftCurve.Evaluate(Mathf.Abs(carVelocity.x) / 70);
			}
			else
			{
				// тут нужны проверки на вытаскивание колес из под земли (само вытаскивание нужно в Suspension)
				
				// тут же идёт управление тангажем в полете
				// нажатие на ускорение дает импульс на передние колеса
				// реверсивная тяга, импульс на задние колеса

				if (Mathf.Abs(moveVertical) > 0.05f && ControlType == ControlType.PLAYER)
				{
					if (moveVertical > 0) // вперед
					{
						Axles[0].LeftSuspension.AddForceToBody(-airForce * fdt);
						Axles[0].RightSuspension.AddForceToBody(-airForce * fdt);
					}
					else // назад
					{
						Axles[1].LeftSuspension.AddForceToBody(-airForce * fdt);
						Axles[1].RightSuspension.AddForceToBody(-airForce * fdt);
					}
				}
			}

			if (EnableCustomGravity) _rb.AddForce(_customGForce);
			
			// limit max speed
			//_rb.velocity = Vector3.ClampMagnitude(_rb.velocity, maxSpeed);
			UpdateCurrentSpeedInfo();
		}

		private void UpdateCurrentSpeedInfo()
		{
			velocitySpeed = _rb.velocity.magnitude;
			
			//tSpeed = Properties.turningBySpeedCurve.Evaluate(velocitySpeed / 10f);
			if (speedUnit == SpeedUnit.mph)
			{

				// 2.23694 is the constant to convert a value from m/s to mph.
				currentSpeed = Mathf.Round(velocitySpeed * 0.223694f);
				//speedText.text = previousText + currentSpeed.ToString() + " mph";
			}
			else 
			{

				// 3.6 is the constant to convert a value from m/s to km/h.
				currentSpeed = Mathf.Round(velocitySpeed * 0.36f);
				//speedText.text = previousText + currentSpeed.ToString() + " km/h";

			}
		}
		
		public void SetTargetPosition(Vector3 targetPos)
		{
			targetPosition = targetPos;
		}
		
		public void AccelerationLogic()
		{
			//helping variables
			speedValue = speedInput * Time.fixedDeltaTime * 1000 * Properties.speedCurve.Evaluate(Mathf.Abs(carVelocity.z) / 100);
			
			if (Properties.separateReverseCurve && carVelocity.z <0 && speedInput < 0)
			{
				speedValue = speedInput * Time.fixedDeltaTime * 1000 * Properties.ReverseCurve.Evaluate(Mathf.Abs(carVelocity.z) / 100);
			}

			var inputAccel = 0f;
			
			switch (ControlType)
			{
				case ControlType.AI:
					inputAccel = _speedAI;
					break;
				case ControlType.PLAYER:
					inputAccel = moveVertical;
					break;
			}

			//speed control
			if (inputAccel is > 0.1f or < -0.1f)
			{
				_rb.AddForceAtPosition(transform.forward * speedValue, groundCheck.position);
			}
		}
		
		public void TurningLogic()
		{
			if (ControlType == ControlType.PLAYER)
			{
				if (carVelocity.z < 0)
				{
					turnInput = -turnInput;
				}
			}
			
			var curvedTurn = Properties.turningBySpeedCurve.Evaluate(_rb.velocity.magnitude/10f);
 			Vector3 desiredTurn = Vector3.up * curvedTurn * turnInput * Time.fixedDeltaTime;
			_rb.AddTorque(desiredTurn, ForceMode.Acceleration);

			float force = turnInput;
			//Debug.Log(turnInput.ToString());
			// дополнительнае силы на колесах
			// в идеале все силы перенести на них
			// при этом не забыть про прижмную силу и козление!!!
			//_rb.AddForceAtPosition(transform.forward * speedValue, transform.position, ForceMode.Force);
			
			foreach (var ax in Axles)
			{
				//if (ax.Steering || ax.Driven)
				if (ax.Steering) // пока подруливаем
				{
					var lw = ax.LeftSuspension; 
					if (lw.IsGrounded)
					{
						lw.AddFrontForceToWheel(force);
					}
				
					var rw = ax.RightSuspension;
					if (rw.IsGrounded)
					{
						rw.AddFrontForceToWheel(force);
					}
				}
			}
		}

		public void FrictionLogic()
		{

			fricValue = frictionForce * Properties.frictionCurve.Evaluate(carVelocity.magnitude / 100);

			Vector3 sideVelocity = carVelocity.x * transform.right;
			Vector3 contactDesiredAccel = -sideVelocity/ Time.fixedDeltaTime;
			float clampedFrictionForce = _rb.mass * contactDesiredAccel.magnitude;
			//if (EnableCustomGravity) _rb.AddForce(Vector3.up * (CustomGravity) * _rb.mass);
			Vector3 gravityForce = CustomGravity * _rb.mass * Vector3.up;
			Vector3 gravityFriction = -Vector3.Project( gravityForce, transform.right);

			Vector3 maxfrictionForce = Vector3.ClampMagnitude(fricValue * 50 * (-sideVelocity.normalized), clampedFrictionForce);
			_rb.AddForceAtPosition(maxfrictionForce + gravityFriction, fricAt.position);

		}

		public void BrakeLogic()
		{
			//brake
			if (carVelocity.z > 1f)
			{
				_rb.AddForceAtPosition(transform.forward * -brakeInput * Time.fixedDeltaTime * 1000, groundCheck.position);
			}
			if (carVelocity.z < -1f)
			{
				_rb.AddForceAtPosition(transform.forward * brakeInput * Time.fixedDeltaTime * 1000, groundCheck.position);
			}
			
			if(carVelocity.magnitude < 1)
			{
				_rb.drag = 5f;
			}
			else
			{
				_rb.drag = 0.15f;
			}
			
		}

		private void UpdateAI()
		{
            if (!_isNeedBooster) { SetTargetPosition(TargetTransform.position); }
            //SetTargetPosition(TargetTransform.position);
			// the new method of calculating turn value
			Vector3 aimedPoint = TargetTransform.position;
			aimedPoint.y = transform.position.y;
			Vector3 aimedDir = (aimedPoint - transform.position).normalized;
			Vector3 myDir = transform.forward;
			myDir.y = 0;
			myDir.Normalize();
			_desiredTurning = Mathf.Abs(Vector3.Angle(myDir, aimedDir));
			
			float reachedTargetDistance = 1f;
			float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
			Vector3 dirToMovePosition = (targetPosition - transform.position).normalized;
			float dot = Vector3.Dot(transform.forward, dirToMovePosition);
			float angleToMove = Vector3.Angle(transform.forward, dirToMovePosition);
			
			if (angleToMove > Properties.brakeAngle || sensorScript.obstacleInPath)
			{
				if(carVelocity.z > 15)
				{
					_brakeAI = 1;
				}
				else
				{
					_brakeAI = 0;
				}
			}
			else
			{
				_brakeAI = 0;
				if (carVelocity.x > 4)
				{
					_brakeAI = 1f*(carVelocity.x/100f);	
				}

			}

			if (distanceToTarget > reachedTargetDistance)
			{
				//Target is still far , keep accelerating 
				if(dot > 0)
				{
					if (_brakeAI == 0)
					{
						_speedAI = _desiredTurning > 15 ? 0.6f : 1f;
					}
					else
					{
						_speedAI = 0f;	
					}
					//float stoppingDistance = 5f;
					//float stoppingSpeed = 5f;
				}
				else
				{
					float reverseDistance = 5f;
					if(distanceToTarget > reverseDistance)
					{
						_speedAI = 1f;
					}
				}

				var angleToDir = Vector3.SignedAngle(transform.forward, dirToMovePosition, Vector3.up);

				if(angleToDir > 3)
				{
					_turnAI = Properties.turnCurve.Evaluate(_desiredTurning / turnAngle);
					//_turnAI = _desiredTurning / turnAngle;
				}
				else if(angleToDir < -3)
				{
					_turnAI = -Properties.turnCurve.Evaluate(_desiredTurning / turnAngle);
					//_turnAI = -(_desiredTurning / turnAngle);
				}
				else
				{
					_turnAI = 0;
				}

			}
			else // reached target
			{
				_turnAI = 0f;
			}
		}

		private void UpdateAIControl()
		{
			if (sensorScript.obstacleInPath == true)
			{
				turnInput = (_stopVehicle == true)? 0 : turnSpeed * -sensorScript.turnmultiplyer;
			}
			else
			{
				turnInput = (_stopVehicle == true) ? 0 : turnSpeed * _turnAI;
			}
			
			speedInput = (_stopVehicle == true) ? 0 : accelForce * _speedAI;
			brakeInput = (_stopVehicle == true) ? brakeValue : brakeValue * -_brakeAI;
			brakeInput *= Mathf.Clamp01(carVelocity.magnitude);
		}
		
		private void UpdateInputControl()
		{
			
			//if (!IsActive) return; 
			
			// Get Input
			moveHorizontal = player.GetAxis("Move Horizontal");
			moveVertical = player.GetAxis("Move Vertical");
			brakePlayerInput = player.GetAxis("Break");
			resetPlayerInput = player.GetAxis("Reset");
/*			
			if (Input.GetKeyUp(KeyCode.F2))
			{
				CameraControllers[_idxActiveCamera].gameObject.SetActive(false);
				_idxActiveCamera++;
				if (_idxActiveCamera == CameraControllers.Count)
				{
					_idxActiveCamera = 0;
				}
				CameraControllers[_idxActiveCamera].gameObject.SetActive(true);
			}
			
			if (Input.GetKeyUp(KeyCode.F5))
			{
				_idxActiveWheels++;
				if (_idxActiveWheels == WheelsProperties.Count)
				{
					_idxActiveWheels = 0;
				}
				//InitializateWheels();
			}
*/			
			//inputs
			//If Use Inverse

			//var curvedTurn = Properties.turningBySpeedCurve.Evaluate(_rb.velocity.magnitude/10f);
			turnInput = _isInverse ? -turnSpeed * moveHorizontal : turnSpeed * moveHorizontal;
			speedInput = accelForce * moveVertical;// * fdt;

			brakeInput = brakeForce * brakePlayerInput;// * fdt;
			brakeInput *= Mathf.Clamp01(carVelocity.magnitude);
		}


		public int CycleActiveCamera()
		{
			var cvc = CameraControllers[_idxActiveCamera].GetComponent<CinemachineVirtualCamera>();
			cvc.Priority = 0;
			CameraControllers[_idxActiveCamera].gameObject.SetActive(false);
			
			
			_idxActiveCamera++;
			if (_idxActiveCamera == CameraControllers.Count)
			{
				_idxActiveCamera = 0;
			}
			
			cvc = CameraControllers[_idxActiveCamera].GetComponent<CinemachineVirtualCamera>();
			cvc.Priority = 24;
			CameraControllers[_idxActiveCamera].gameObject.SetActive(true);
			
			return _idxActiveCamera;
		}
		
		
		public void TireVisuals()
		{
			//Tire mesh rotate
			foreach (var ax in Axles)
			{
				// TODO: BAD!!!
				if (ax.LeftWheel.tyre == null) return;
				if (ax.RightWheel.tyre == null) return;
				UpdateTireVisual(ax, ax.LeftWheel.tyre.transform);
				UpdateTireVisual(ax, ax.RightWheel.tyre.transform);

				UpdateDrawMarks(ax.LeftSuspension.IsGrounded, ax.LeftWheel.skids);
				UpdateDrawMarks(ax.RightSuspension.IsGrounded, ax.RightWheel.skids);
				
			}
		}

		private void UpdateDrawMarks(bool isGrounded, SkidMarks skidMarks)
		{
			if (isGrounded)
			{
				if (Mathf.Abs(carVelocity.x) > SkidEnable)
				{
					skidMarks.DrawMarks(true);
					
				}
				else
				{
					skidMarks.DrawMarks(false);					}
			}
			else
			{
				skidMarks.DrawMarks(false);
			}
		}
		
		
		private void UpdateTireVisual(CarAxle ax, Transform axWheel)
		{
			axWheel.RotateAround(axWheel.position, axWheel.right, carVelocity.z/0.63f);
			axWheel.localPosition = Vector3.zero;

			if (ax.Steering)
			{
				var axSteer = axWheel.parent.parent;
				
				axSteer.localRotation = Quaternion.Slerp(axSteer.localRotation, Quaternion.Euler(axSteer.localRotation.eulerAngles.x,
					turnAngle * moveHorizontal, axSteer.localRotation.eulerAngles.z), slerpTime);
			}
		}
		
		public void AudioControl()
		{
			
			if (IsGrounded)
			{
				if (Mathf.Abs(carVelocity.x) > SkidEnable - 0.1f)
				{
					if (ControlType == ControlType.PLAYER)
					{
						brakesFMOD.SetParameter("BrakeBool", 1);
						if (!brakesFMOD.IsPlaying()) brakesFMOD.Play();
						//audioTire.mute = false;
					}
				}
				else
				{
					brakesFMOD.SetParameter("BrakeBool", 0);
					//brakesFMOD.Stop();
					//audioTire.mute = true;
				}
			}
			else
			{
				//brakesFMOD.Stop();
				brakesFMOD.SetParameter("BrakeBool", 0);
				//audioTire.mute = true;
			}

		}
		
		private void ResetAuto()
		{
			var verticalAngle = Vector3.Angle(_rb.transform.up, Vector3.up);
			if (verticalAngle >= 80 && velocitySpeed <= 1f)
			{
				/*
				var transformCar = _rb.transform;
				var rotationTemp = transformCar.localRotation.eulerAngles;
				// reset!
				transformCar.position += Vector3.up*.5f;
				//_rb.transform.localRotation = Quaternion.Euler(rotationTemp.x, rotationTemp.y, 0); 
				_rb.transform.localRotation = Quaternion.Euler(0, rotationTemp.y, 0); 
				*/
				Recovery();
			}
		}

		public void Recovery()
		{
			if (CarState != CarState.RACE) return;
			
			if (!IsFirstCheckpointPassed) return;
			
			var way = GetComponent<WaypointProgressTracker>();
			if (way != null)
			{
				CarState = CarState.RECOVERY;

				var point = way.GetCurrentRoutePosition();
				var transformCar = _rb.transform;
				Vector3 targetRandomPosition = Random.onUnitSphere*10f;
				targetRandomPosition.y = 0;
				transformCar.position = point.position+Vector3.up*.25f+targetRandomPosition;
				_rb.velocity = Vector3.zero;
				_rb.transform.rotation = Quaternion.LookRotation(point.direction);
				_rb.isKinematic = true;
				_rb.detectCollisions = false;
				//стартуем эффект и таймер рекавери
				// пока корутиной
				StartCoroutine(RecoverWait());
			}
		}
		
		IEnumerator RecoverWait()
		{
			yield return new WaitForSeconds(1f);
			_rb.isKinematic = false;
			_rb.detectCollisions = true;
			CarState = CarState.RACE;
		}

		void AudioForFakeGearChange()
        {
			// Fake changing gears logic 
            if (currentSpeed != 0)
            {
				float speedCheckpoint = 0.0f;
				if (velocitySpeed <=100) speedCheckpoint = velocitySpeed*0.7f / maxSpeed;
                var points = speedCheckCurve.keys;
                for (int i = points.Length - 1; i > 0; i--)
                {
                    if (points[i].value < speedCheckpoint)
                    {
                        if (currentGear != i)
                        {
                            currentGear = i;
                            gearText.text = currentGear.ToString();
                            pressAccelerateTime = 0;
                            engineFMOD.SetParameter("Boost", 0);
                        }
                        break;
                    }
                }
            }


			// Input of arrow key or W key need to change because project use new input system
			if (ControlType == ControlType.PLAYER)
            {
				if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
				{
					fakeBoost = true;
				}
				if (Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
				{
					fakeBoost = false;
				}
			}


			// This is not working need to came up with something else 
/*            if (_rb.velocity.magnitude > 1) fakeBoost = true;
			else fakeBoost = false;*/


			// Using time of keys pressed to emulate sound of an acceleration according to an array of animation curves
			if (fakeBoost && IsGrounded)
            {
				if (!engineFMOD.IsPlaying()) engineFMOD.Play();

				pressAccelerateTime += Time.deltaTime*0.1f; 
				int temppress = (int)pressAccelerateTime;

				textEngine.text = (velocitySpeed * 0.7f / maxSpeed).ToString();
				RPM = gearChangeRPMCurves[currentGear].Evaluate(pressAccelerateTime - temppress); // Progress within range of 1
				engineFMOD.SetParameter("RPM", RPM);

				var pitchShift = rpmRise.Evaluate((velocitySpeed * 0.5f / maxSpeed));
					engineFMOD.SetParameter("Boost", Random.Range(pitchShift - 10,pitchShift + 10) );
			}
			else
            {
				pressAccelerateTime = 0;
				RPM -= Time.deltaTime*0.3f;
				if (RPM >= 0 ) engineFMOD.SetParameter("RPM", RPM );
				engineFMOD.SetParameter("Boost", 0);
				textEngine.text = pressAccelerateTime.ToString();
				if (currentSpeed <= 1)
                {
					currentGear = 0;
				}
			}

			//Update Speed to make brakes sound audible need to change
			//brakesFMOD.SetParameter("Speed", currentSpeed / maxSpeed);

			// Tires spinning sound
			if (_rb.velocity.magnitude > 1)
				{
				if (!tiresFMOD.IsPlaying()) tiresFMOD.Play();
				tiresFMOD.SetParameter("Speed", currentSpeed / maxSpeed);
				}
            else tiresFMOD.Stop();
			

			//Audio logic for turns
			if (Mathf.Abs(turnInput) > 500 && !tempTurnInput)
			{
				if (!turnFMOD.IsPlaying()) turnFMOD.Play();
				tempTurnInput = true;
			}
			else
			{
				if (Mathf.Abs(turnInput) < 500) tempTurnInput = false;
			}
		}


		// Examples of mix changing function
		public void EngineVolumeChange(float engineVol)
        {
			EngineBus.setVolume(engineVol);
		}

		public void BrakesVolumeChange(float brakesVol)
		{
			BrakesBus.setVolume(brakesVol);
		}		
		
		public void TiresVolumeChange(float tiresVol)
		{
			TiresBus.setVolume(tiresVol);
		}
	}
}