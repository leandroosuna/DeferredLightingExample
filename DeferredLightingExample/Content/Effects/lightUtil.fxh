
float3 lightAmbientColor; // Light's Ambient Color
float3 lightDiffuseColor; // Light's Diffuse Color
float3 lightSpecularColor; // Light's Specular Color

float3 lightPosition;
float3 cameraPosition; // Camera position
float KA;
float KD;
float KS;
float shininess;

float3 getPixelColor(float3 baseColor, float3 worldPos, float3 normal)
{
	float3 output;
	float3 lightDirection = normalize(lightPosition - worldPos);
	float3 viewDirection = normalize(cameraPosition - worldPos);
	float3 halfVector = normalize(lightDirection + viewDirection);
    
	float NdotL = saturate(dot(normal, lightDirection));
	float3 diffuseLight = KD * lightDiffuseColor * NdotL;
	float NdotH = dot(normal, halfVector);
	float3 specularLight = sign(NdotL) * KS * lightSpecularColor * pow(saturate(NdotH), shininess);
	return saturate(lightAmbientColor * KA + diffuseLight) * baseColor + specularLight;
}

float3 getPixelAmbient(float3 worldPos, float3 normal, float KD, float KS, float shininess)
{
    float3 output;
    float3 lightDirection = normalize(lightPosition - float3(0, 0, 0));
    float3 viewDirection = normalize(cameraPosition - worldPos);
    float3 halfVector = normalize(lightDirection + viewDirection);
    
    float NdotL = saturate(dot(normal, lightDirection));
    float3 diffuseLight = KD * lightDiffuseColor * NdotL;
    float NdotH = dot(normal, halfVector);
    float3 specularLight = sign(NdotL) * KS * lightSpecularColor * pow(saturate(NdotH), shininess);
    return diffuseLight *1.3 + specularLight;
}


float3 getPixelColorNoAmbient(float3 worldPos, float3 normal, float KD, float KS, float shininess)
{
	float3 output;
	float3 lightDirection = normalize(lightPosition - worldPos);
	float3 viewDirection = normalize(cameraPosition - worldPos);
	float3 halfVector = normalize(lightDirection + viewDirection);
    
	float NdotL = saturate(dot(normal, lightDirection));
	float3 diffuseLight = KD * lightDiffuseColor * NdotL;
	float NdotH = dot(normal, halfVector);
	float3 specularLight = sign(NdotL) * KS * lightSpecularColor * pow(saturate(NdotH), shininess);
	return diffuseLight+ specularLight;
}
