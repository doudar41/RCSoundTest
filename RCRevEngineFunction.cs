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
private float rpmCoefficient;

public int currentGear;
float RPM;

//Changes to Audio Control function
public void AudioControl()
{

	//audios
	if (IsGrounded)
	{
		if (Mathf.Abs(carVelocity.x) > SkidEnable - 0.1f)
		{
			if (ControlType == ControlType.PLAYER)
			{
				brakesFMOD.SetParameter("BrakeBool", 1);
				if (!brakesFMOD.IsPlaying()) brakesFMOD.Play();
			}
		}
		else
		{
			brakesFMOD.SetParameter("BrakeBool", 0);
		}
	}
	else
	{
		brakesFMOD.SetParameter("BrakeBool", 0);
	}

}
		



void GearChangeEmulator()
        {
	
	// Gear switching check if speed / maxSpeed is more than value of a point on animation curve
		if (currentSpeed != 0)
		{
			float speedCheckpoint = currentSpeed / maxSpeed;
			var points = speedCheckCurve.keys;
			for (int i = points.Length-1; i > 0; i--)
			{
				if (points[i].value < speedCheckpoint)
				{
					if (currentGear != i)
		{
						currentGear = i;
						pressAccelarateTime = 0;
						engineFMOD.SetParameter("Boost", 0);
					}
					break;
				}
			}
		}
			
	//Toggle count of pressing accelerate button
	
			if (Input.GetKeyDown(KeyCode.UpArrow))
            {
				fakeBoost = true;
			}
			if (Input.GetKeyUp(KeyCode.UpArrow))
            {
				fakeBoost = false;
			}

	// Count time of pressing accelerate button
	
			if (fakeBoost && IsGrounded)
            		{
				pressAccelarateTime += Time.deltaTime*0.2f;
				RPM = gearChangeRPMCurves[currentGear].Evaluate(pressAccelarateTime) * rpmCoefficient;
				//Randomize RPM
				engineFMOD.SetParameter("RPM", Random.Range(RPM - 10,RPM + 10));
				
				//additional pitch
				if(pressAccelarateTime != 0)
                		{
				var pitchShift = rpmRise.Evaluate(1/pressAccelarateTime);
				engineFMOD.SetParameter("Boost", Random.Range(pitchShift - 10,pitchShift + 10) );
               			 }
			}
			else
			{
			//slowly return to idle
			pressAccelarateTime = 0;
			engineFMOD.SetParameter("RPM", currentSpeed);
			engineFMOD.SetParameter("Boost", 0);
			if (currentSpeed <= 1)
				{
				currentGear = 0;
				}
			}
		} 
		
		// Example of mix changing function
		public void EngineVolumeChange(float engineVol)
   		{
			EngineBus.setVolume(engineVol);
		}

		public void BrakesVolumeChange(float brakesVol)
		{
			BrakesBus.setVolume(brakesVol);
		}
