namespace UABEANext4.Controls.SceneView;

public static class SceneViewShaders
{
    // Vertex shader with texture coordinates
    public const string VERTEX_SOURCE = @"#version 300 es
precision highp float;
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uProjection;
uniform mat4 uView;

out vec3 FragPos;
out vec3 FragNormal;
out vec2 TexCoord;

void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    FragPos = worldPos.xyz;
    gl_Position = uProjection * uView * worldPos;
    FragNormal = mat3(transpose(inverse(uModel))) * aNormal;
    TexCoord = aTexCoord;
}";

    // Fragment shader with texture and lighting
    public const string FRAGMENT_SOURCE = @"#version 300 es
precision highp float;

in vec3 FragPos;
in vec3 FragNormal;
in vec2 TexCoord;

out vec4 FragColor;

uniform vec3 uDirectionalLightDir;
uniform vec3 uDirectionalLightColor;
uniform vec3 uCameraPos;
uniform sampler2D uTexture;
uniform bool uHasTexture;
uniform bool uIsSelected;
uniform vec3 uBaseColor;

void main()
{
    vec3 normal = normalize(FragNormal);
    vec3 lightDirection = normalize(-uDirectionalLightDir);

    // Ambient
    vec3 ambient = 0.3 * uDirectionalLightColor;

    // Diffuse
    float diff = max(dot(normal, lightDirection), 0.0);
    vec3 diffuse = diff * uDirectionalLightColor;

    // Specular
    vec3 viewDir = normalize(uCameraPos - FragPos);
    vec3 reflectDir = reflect(-lightDirection, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    vec3 specular = 0.2 * spec * uDirectionalLightColor;

    // Get base color from texture or uniform
    vec3 objectColor;
    if (uHasTexture) {
        objectColor = texture(uTexture, TexCoord).rgb;
    } else {
        objectColor = uBaseColor;
    }

    vec3 result = (ambient + diffuse + specular) * objectColor;

    // Selection highlight
    if (uIsSelected) {
        result = mix(result, vec3(1.0, 0.6, 0.0), 0.3);
    }

    FragColor = vec4(result, 1.0);
}";

    // Simple shader for grid rendering
    public const string GRID_VERTEX_SOURCE = @"#version 300 es
precision highp float;
layout (location = 0) in vec3 aPos;

uniform mat4 uProjection;
uniform mat4 uView;

out vec3 FragPos;

void main()
{
    FragPos = aPos;
    gl_Position = uProjection * uView * vec4(aPos, 1.0);
}";

    public const string GRID_FRAGMENT_SOURCE = @"#version 300 es
precision highp float;

in vec3 FragPos;
out vec4 FragColor;

uniform float uGridSize;
uniform vec3 uGridColor;

void main()
{
    // Calculate grid lines
    float lineWidth = 0.02;
    vec2 coord = FragPos.xz / uGridSize;
    vec2 grid = abs(fract(coord - 0.5) - 0.5) / fwidth(coord);
    float line = min(grid.x, grid.y);
    float alpha = 1.0 - min(line, 1.0);

    // Fade out with distance
    float dist = length(FragPos.xz);
    float fade = 1.0 - smoothstep(50.0, 100.0, dist);

    if (alpha < 0.1) discard;

    FragColor = vec4(uGridColor, alpha * fade * 0.5);
}";

    // Gizmo shader for move handles
    public const string GIZMO_VERTEX_SOURCE = @"#version 300 es
precision highp float;
layout (location = 0) in vec3 aPos;

uniform mat4 uModel;
uniform mat4 uProjection;
uniform mat4 uView;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
}";

    public const string GIZMO_FRAGMENT_SOURCE = @"#version 300 es
precision highp float;

out vec4 FragColor;
uniform vec3 uColor;

void main()
{
    FragColor = vec4(uColor, 1.0);
}";

    public const int POSITION_LOC = 0;
    public const int NORMAL_LOC = 1;
    public const int TEXCOORD_LOC = 2;
}
