void GearChangeEmulator()
        {
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
							gearText.text = currentGear.ToString();
							pressAccelarateTime = 0;
							engineFMOD.SetParameter("Boost", 0);
						}
						break;
					}
				}
			}
			
			if (Input.GetKeyDown(KeyCode.UpArrow))
            {
				fakeBoost = true;
			}
			if (Input.GetKeyUp(KeyCode.UpArrow))
            {
				fakeBoost = false;
			}

			if (fakeBoost && IsGrounded)
            {
				pressAccelarateTime += Time.deltaTime*0.2f;
				//textEngine.text = gearChangeRPMCurves[currentGear].Evaluate(pressAccelarateTime).ToString();
				textEngine.text = currentSpeed.ToString();
				RPM = gearChangeRPMCurves[currentGear].Evaluate(pressAccelarateTime) * rpmCoefficient;


				engineFMOD.SetParameter("RPM", Random.Range(RPM - 10,RPM + 10));
				if(pressAccelarateTime != 0)
                {
				var pitchShift = rpmRise.Evaluate(1/pressAccelarateTime);
					engineFMOD.SetParameter("Boost", Random.Range(pitchShift - 10,pitchShift + 10) );
                }


			}
			else
            {
				pressAccelarateTime = 0;
				engineFMOD.SetParameter("RPM", currentSpeed);
				engineFMOD.SetParameter("Boost", 0);
				textEngine.text = pressAccelarateTime.ToString();
				if (currentSpeed <= 1)
                {
					currentGear = 0;
				}
			}
		} 

		public void EngineVolumeChange(float engineVol)
        {
			EngineBus.setVolume(engineVol);

		}

		public void BrakesVolumeChange(float brakesVol)
		{
			BrakesBus.setVolume(brakesVol);
		}
