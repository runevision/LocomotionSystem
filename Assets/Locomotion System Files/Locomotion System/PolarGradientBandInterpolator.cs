/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

public class PolarGradientBandInterpolator : Interpolator {
	
	public PolarGradientBandInterpolator(float[][] samplePoints) : base(samplePoints) {
		samples = samplePoints;
	}
	
	public override float[] Interpolate(float[] output, bool normalize) {
		float[] weights = BasicChecks(output);
		if (weights!=null) return weights;
		weights = new float[samples.Length];
		
		Vector3 outp;
		Vector3[] samp = new Vector3[samples.Length];
		if (output.Length==2) {
			outp = new Vector3(output[0],output[1],0);
			for (int i=0; i<samples.Length; i++) {
				samp[i] = new Vector3(samples[i][0],samples[i][1],0);
			}
		}
		else if (output.Length==3) {
			outp = new Vector3(output[0],output[1],output[2]);
			for (int i=0; i<samples.Length; i++) {
				samp[i] = new Vector3(samples[i][0],samples[i][1],samples[i][2]);
			}
		}
		else return null;
		
		for (int i=0; i<samples.Length; i++) {
			bool outsideHull = false;
			float value = 1;
			for (int j=0; j<samples.Length; j++) {
				if (i==j) continue;
				
				Vector3 sampleI = samp[i];
				Vector3 sampleJ = samp[j];
				
				float iAngle, oAngle;
				Vector3 outputProj;
				float angleMultiplier = 2;
				if (sampleI==Vector3.zero) {
					iAngle = Vector3.Angle(outp,sampleJ)*Mathf.Deg2Rad;
					oAngle = 0;
					outputProj = outp;
					angleMultiplier = 1;
				}
				else if (sampleJ==Vector3.zero) {
					iAngle = Vector3.Angle(outp,sampleI)*Mathf.Deg2Rad;
					oAngle = iAngle;
					outputProj = outp;
					angleMultiplier = 1;
				}
				else {
					iAngle = Vector3.Angle(sampleI,sampleJ)*Mathf.Deg2Rad;
					if (iAngle>0) {
						if (outp==Vector3.zero) {
							oAngle = iAngle;
							outputProj = outp;
						}
						else {
							Vector3 axis = Vector3.Cross(sampleI,sampleJ);
							outputProj = Util.ProjectOntoPlane(outp,axis);
							oAngle = Vector3.Angle(sampleI,outputProj)*Mathf.Deg2Rad;
							if (iAngle<Mathf.PI*0.99f) {
								if (Vector3.Dot(Vector3.Cross(sampleI,outputProj),axis)<0) {
									oAngle *= -1;
								}
							}
						}
					}
					else {
						outputProj = outp;
						oAngle = 0;
					}
				}
				
				float magI = sampleI.magnitude;
				float magJ = sampleJ.magnitude;
				float magO = outputProj.magnitude;
				float avgMag = (magI+magJ)/2;
				magI /= avgMag;
				magJ /= avgMag;
				magO /= avgMag;
				Vector3 vecIJ = new Vector3(iAngle*angleMultiplier, magJ-magI, 0);
				Vector3 vecIO = new Vector3(oAngle*angleMultiplier, magO-magI, 0);
				
				float newValue = 1-Vector3.Dot(vecIJ,vecIO)/vecIJ.sqrMagnitude;
				
				if (newValue < 0) {
					outsideHull = true;
					break;
				}
				value = Mathf.Min(value, newValue);
			}
			if (!outsideHull) weights[i] = value;
		}
		
		// Normalize weights
		if (normalize) {
			float summedWeight = 0;
			for (int i=0; i<samples.Length; i++) summedWeight += weights[i];
			if (summedWeight > 0)
				for (int i=0; i<samples.Length; i++) weights[i] /= summedWeight;
		}
		
		return weights;
	}
	
}
