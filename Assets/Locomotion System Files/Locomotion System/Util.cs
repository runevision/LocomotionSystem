/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

public class Util {
	
	public static bool IsSaneNumber(float f) {
		if (System.Single.IsNaN(f)) return false;
		if (f==Mathf.Infinity) return false;
		if (f==Mathf.NegativeInfinity) return false;
		if (f>1000000000000) return false;
		if (f<-1000000000000) return false;
		return true;
	}
	
	public static Vector3 Clamp(Vector3 v, float length) {
		float l = v.magnitude;
		if (l > length) return v / l * length;
		return v;
	}
	
	public static float Mod(float x, float period) {
		float r = x % period;
		return (r>=0?r:r+period);
	}
	public static int Mod(int x, int period) {
		int r = x % period;
		return (r>=0?r:r+period);
	}
	public static float Mod(float x) { return Mod(x, 1); }
	public static int Mod(int x) { return Mod(x, 1); }
	
	public static float CyclicDiff(float high, float low, float period, bool skipWrap) {
		if (!skipWrap) {
			high = Mod(high,period);
			low = Mod(low,period);
		}
		return ( high>=low ? high-low : high+period-low );
	}
	public static int CyclicDiff(int high, int low, int period, bool skipWrap) {
		if (!skipWrap) {
			high = Mod(high,period);
			low = Mod(low,period);
		}
		return ( high>=low ? high-low : high+period-low );
	}
	public static float CyclicDiff(float high, float low, float period) { return CyclicDiff(high, low, period, false); }
	public static int CyclicDiff(int high, int low, int period) { return CyclicDiff(high, low, period, false); }
	public static float CyclicDiff(float high, float low) { return CyclicDiff(high, low, 1, false); }
	public static int CyclicDiff(int high, int low) { return CyclicDiff(high, low, 1, false); }
	
	// Returns true is compared is lower than comparedTo relative to reference,
	// which is assumed not to lie between compared and comparedTo.
	public static bool CyclicIsLower(float compared, float comparedTo, float reference, float period) {
		compared = Mod(compared,period);
		comparedTo = Mod(comparedTo,period);
		if (
			CyclicDiff(compared,reference,period,true)
			<
			CyclicDiff(comparedTo,reference,period,true)
		) return true;
		return false;
	}
	public static bool CyclicIsLower(int compared, int comparedTo, int reference, int period) {
		compared = Mod(compared,period);
		comparedTo = Mod(comparedTo,period);
		if (
			CyclicDiff(compared,reference,period,true)
			<
			CyclicDiff(comparedTo,reference,period,true)
		) return true;
		return false;
	}
	public static bool CyclicIsLower(float compared, float comparedTo, float reference) {
		return CyclicIsLower(compared, comparedTo, reference, 1); }
	public static bool CyclicIsLower(int compared, int comparedTo, int reference) {
		return CyclicIsLower(compared, comparedTo, reference, 1); }
	
	public static float CyclicLerp(float a, float b, float t, float period) {
		if (Mathf.Abs(b-a)<=period/2) { return a*(1-t)+b*t; }
		if (b<a) a -= period; else b -= period;
		return Util.Mod(a*(1-t)+b*t);
	}
	
	public static Vector3 ProjectOntoPlane(Vector3 v, Vector3 normal) {
		return v-Vector3.Project(v,normal);
	}
	
	public static Vector3 SetHeight(Vector3 originalVector, Vector3 referenceHeightVector, Vector3 upVector) {
		Vector3 originalOnPlane = ProjectOntoPlane(originalVector, upVector);
		Vector3 referenceOnAxis = Vector3.Project(referenceHeightVector, upVector);
		return originalOnPlane + referenceOnAxis;
	}
	
	public static Vector3 GetHighest(Vector3 a, Vector3 b, Vector3 upVector) {
		if (Vector3.Dot(a,upVector) >= Vector3.Dot(b,upVector)) return a;
		return b;
	}
	public static Vector3 GetLowest(Vector3 a, Vector3 b, Vector3 upVector) {
		if (Vector3.Dot(a,upVector) <= Vector3.Dot(b,upVector)) return a;
		return b;
	}
	
	public static Matrix4x4 RelativeMatrix(Transform t, Transform relativeTo) {
		return relativeTo.worldToLocalMatrix * t.localToWorldMatrix;
	}
	
	public static Vector3 TransformVector(Matrix4x4 m, Vector3 v) {
		return m.MultiplyPoint(v) - m.MultiplyPoint(Vector3.zero);
	}
	public static Vector3 TransformVector(Transform t, Vector3 v) {
		return TransformVector(t.localToWorldMatrix,v);
	}
	
	public static void TransformFromMatrix(Matrix4x4 matrix, Transform trans) {
		trans.rotation = Util.QuaternionFromMatrix(matrix);
		trans.position = matrix.GetColumn(3); // uses implicit conversion from Vector4 to Vector3
	}
	
	public static Quaternion QuaternionFromMatrix(Matrix4x4 m) {
		// Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
		Quaternion q = new Quaternion();
		q.w = Mathf.Sqrt( Mathf.Max( 0, 1 + m[0,0] + m[1,1] + m[2,2] ) ) / 2; 
		q.x = Mathf.Sqrt( Mathf.Max( 0, 1 + m[0,0] - m[1,1] - m[2,2] ) ) / 2; 
		q.y = Mathf.Sqrt( Mathf.Max( 0, 1 - m[0,0] + m[1,1] - m[2,2] ) ) / 2; 
		q.z = Mathf.Sqrt( Mathf.Max( 0, 1 - m[0,0] - m[1,1] + m[2,2] ) ) / 2; 
		q.x *= Mathf.Sign( q.x * ( m[2,1] - m[1,2] ) );
		q.y *= Mathf.Sign( q.y * ( m[0,2] - m[2,0] ) );
		q.z *= Mathf.Sign( q.z * ( m[1,0] - m[0,1] ) );
		return q;
	}
	
	public static Matrix4x4 MatrixFromQuaternion(Quaternion q) {
		return CreateMatrix(q*Vector3.right, q*Vector3.up, q*Vector3.forward, Vector3.zero);
	}
	
	public static Matrix4x4 MatrixFromQuaternionPosition(Quaternion q, Vector3 p) {
		Matrix4x4 m = MatrixFromQuaternion(q);
		m.SetColumn(3,p);
		m[3,3] = 1;
		return m;
	}
	
	public static Matrix4x4 MatrixSlerp(Matrix4x4 a, Matrix4x4 b, float t) {
		t = Mathf.Clamp01(t);
		Matrix4x4 m = MatrixFromQuaternion(Quaternion.Slerp(QuaternionFromMatrix(a),QuaternionFromMatrix(b),t));
		m.SetColumn(3,a.GetColumn(3)*(1-t)+b.GetColumn(3)*t);
		m[3,3] = 1;
		return m;
	}
	
	public static Matrix4x4 CreateMatrix(Vector3 right, Vector3 up, Vector3 forward, Vector3 position) {
		Matrix4x4 m = Matrix4x4.identity;
		m.SetColumn(0,right);
		m.SetColumn(1,up);
		m.SetColumn(2,forward);
		m.SetColumn(3,position);
		m[3,3] = 1;
		return m;
	}
	public static Matrix4x4 CreateMatrixPosition(Vector3 position) {
		Matrix4x4 m = Matrix4x4.identity;
		m.SetColumn(3,position);
		m[3,3] = 1;
		return m;
	}
	public static void TranslateMatrix(ref Matrix4x4 m, Vector3 position) {
		m.SetColumn(3,(Vector3)(m.GetColumn(3))+position);
		m[3,3] = 1;
	}
	
	public static Vector3 ConstantSlerp(Vector3 from, Vector3 to, float angle) {
		float value = Mathf.Min(1, angle / Vector3.Angle(from, to));
		return Vector3.Slerp(from, to, value);
	}
	public static Quaternion ConstantSlerp(Quaternion from, Quaternion to, float angle) {
		float value = Mathf.Min(1, angle / Quaternion.Angle(from, to));
		return Quaternion.Slerp(from, to, value);
	}
	public static Vector3 ConstantLerp(Vector3 from, Vector3 to, float length) {
		return from + Clamp(to-from, length);
	}
	
	public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) {
		Vector3 ab = Vector3.Lerp(a,b,t);
		Vector3 bc = Vector3.Lerp(b,c,t);
		Vector3 cd = Vector3.Lerp(c,d,t);
		Vector3 abc = Vector3.Lerp(ab,bc,t);
		Vector3 bcd = Vector3.Lerp(bc,cd,t);
		return Vector3.Lerp(abc,bcd,t);
	}
	
	public static GameObject Create3dText(Font font, string text, Vector3 position, float size, Color color) {
		// Create new object to display 3d text
		GameObject myTextObject = new GameObject("text_"+text);
		
		// Add TextMesh and MeshRenderer components
		TextMesh textMeshComponent = myTextObject.AddComponent(typeof(TextMesh)) as TextMesh;
		myTextObject.AddComponent(typeof(MeshRenderer));
		
		// Set font of TextMesh component (it works according to the inspector)
		textMeshComponent.font = font;
		myTextObject.GetComponent<Renderer>().material = font.material;
		myTextObject.GetComponent<Renderer>().material.color = color;
		
		// Set the text string of the TextMesh component (it works according to the inspector)
		textMeshComponent.text = text;
		
		myTextObject.transform.localScale = Vector3.one*size;
		myTextObject.transform.Translate(position);
		
		return myTextObject;
	}
	
	public static float[] GetLineSphereIntersections(Vector3 lineStart, Vector3 lineDir, Vector3 sphereCenter, float sphereRadius) {
		/*double a = lineDir.sqrMagnitude;
		double b = 2 * (Vector3.Dot(lineStart, lineDir) - Vector3.Dot(lineDir, sphereCenter));
		double c = lineStart.sqrMagnitude + sphereCenter.sqrMagnitude - 2*Vector3.Dot(lineStart, sphereCenter) - sphereRadius*sphereRadius;
		double d = b*b - 4*a*c;
		if (d<0) return null;
		double i1 = (-b - System.Math.Sqrt(d)) / (2*a);
		double i2 = (-b + System.Math.Sqrt(d)) / (2*a);
		if (i1<i2) return new float[] {(float)i1, (float)i2};
		else       return new float[] {(float)i2, (float)i1};*/
		
		float a = lineDir.sqrMagnitude;
		float b = 2 * (Vector3.Dot(lineStart, lineDir) - Vector3.Dot(lineDir, sphereCenter));
		float c = lineStart.sqrMagnitude + sphereCenter.sqrMagnitude - 2*Vector3.Dot(lineStart, sphereCenter) - sphereRadius*sphereRadius;
		float d = b*b - 4*a*c;
		if (d<0) return null;
		float i1 = (-b - Mathf.Sqrt(d)) / (2*a);
		float i2 = (-b + Mathf.Sqrt(d)) / (2*a);
		if (i1<i2) return new float[] {i1, i2};
		else       return new float[] {i2, i1};
	}
}

public class DrawArea {
	public Vector3 min;
	public Vector3 max;
	public Vector3 canvasMin = new Vector3(0,0,0);
	public Vector3 canvasMax = new Vector3(1,1,1);
	//public float drawDistance = 1;
	
	public DrawArea(Vector3 min, Vector3 max) {
		this.min = min;
		this.max = max;
	}
	
	public virtual Vector3 Point(Vector3 p) {
		return Camera.main.ScreenToWorldPoint(
			Vector3.Scale(
				new Vector3(
					(p.x-canvasMin.x) / (canvasMax.x-canvasMin.x),
					(p.y-canvasMin.y) / (canvasMax.y-canvasMin.y),
					0
				),
				max-min
			)+min
			+Vector3.forward * Camera.main.nearClipPlane *1.1f
		);
	}
	
	public void DrawLine(Vector3 a, Vector3 b, Color c) {
		GL.Color(c);
		GL.Vertex(Point(a));
		GL.Vertex(Point(b));
	}
	
	public void DrawRay(Vector3 start, Vector3 dir, Color c) {
		DrawLine(start, start+dir, c);
	}
	
	public void DrawRect(Vector3 a, Vector3 b, Color c) {
		GL.Color(c);
		GL.Vertex(Point(new Vector3(a.x,a.y,0)));
		GL.Vertex(Point(new Vector3(a.x,b.y,0)));
		GL.Vertex(Point(new Vector3(b.x,b.y,0)));
		GL.Vertex(Point(new Vector3(b.x,a.y,0)));
	}
	
	public void DrawDiamond(Vector3 a, Vector3 b, Color c) {
		GL.Color(c);
		GL.Vertex(Point(new Vector3(a.x,(a.y+b.y)/2,0)));
		GL.Vertex(Point(new Vector3((a.x+b.x)/2,b.y,0)));
		GL.Vertex(Point(new Vector3(b.x,(a.y+b.y)/2,0)));
		GL.Vertex(Point(new Vector3((a.x+b.x)/2,a.y,0)));
	}
	
	public void DrawRect(Vector3 corner, Vector3 dirA, Vector3 dirB, Color c) {
		GL.Color(c);
		Vector3[] dirs = new Vector3[]{dirA,dirB};
		for (int i=0; i<2; i++) {
			for (int dir=0; dir<2; dir++) {
				Vector3 start = corner + dirs[(dir+1)%2]*i;
				GL.Vertex(Point(start));
				GL.Vertex(Point(start+dirs[dir]));
			}
		}
	}
	
	public void DrawCube(Vector3 corner, Vector3 dirA, Vector3 dirB, Vector3 dirC, Color c) {
		GL.Color(c);
		Vector3[] dirs = new Vector3[]{dirA,dirB,dirC};
		for (int i=0; i<2; i++) {
			for (int j=0; j<2; j++) {
				for (int dir=0; dir<3; dir++) {
					Vector3 start = corner + dirs[(dir+1)%3]*i + dirs[(dir+2)%3]*j;
					GL.Vertex(Point(start));
					GL.Vertex(Point(start+dirs[dir]));
				}
			}
		}
	}
}

public class DrawArea3D: DrawArea {
	public Matrix4x4 matrix;
	
	public DrawArea3D(Vector3 min, Vector3 max, Matrix4x4 matrix): base(min,max) {
		this.matrix = matrix;
	}
	
	public override Vector3 Point(Vector3 p) {
		return matrix.MultiplyPoint3x4(
			Vector3.Scale(
				new Vector3(
					(p.x-canvasMin.x) / (canvasMax.x-canvasMin.x),
					(p.y-canvasMin.y) / (canvasMax.y-canvasMin.y),
					p.z
				),
				max-min
			)+min
		);
	}
}


public class SmoothFollower {
	
	private Vector3 targetPosition;
	private Vector3 position;
	private Vector3 velocity;
	private float smoothingTime;
	private float prediction;
	
	public SmoothFollower(float smoothingTime) {
		targetPosition = Vector3.zero;
		position = Vector3.zero;
		velocity = Vector3.zero;
		this.smoothingTime = smoothingTime;
		prediction = 1;
	}
	
	public SmoothFollower(float smoothingTime, float prediction) {
		targetPosition = Vector3.zero;
		position = Vector3.zero;
		velocity = Vector3.zero;
		this.smoothingTime = smoothingTime;
		this.prediction = prediction;
	}
	
	// Update should be called once per frame
	public Vector3 Update(Vector3 targetPositionNew, float deltaTime) {
		Vector3 targetVelocity = (targetPositionNew-targetPosition)/deltaTime;
		targetPosition = targetPositionNew;
		
		float d = Mathf.Min(1,deltaTime/smoothingTime);
		velocity = velocity*(1-d) + (targetPosition+targetVelocity*prediction-position)*d;
		
		position += velocity*Time.deltaTime;
		return position;
	}
	
	public Vector3 Update(Vector3 targetPositionNew, float deltaTime, bool reset) {
		if (reset) {
			targetPosition = targetPositionNew;
			position = targetPositionNew;
			velocity = Vector3.zero;
			return position;
		}
		return Update(targetPositionNew, deltaTime);
	}
	
	public Vector3 GetPosition() { return position; }
	public Vector3 GetVelocity() { return velocity; }
}