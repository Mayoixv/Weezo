import pyodbc
import sys
import requests
import json
import time

sys.stdout.reconfigure(encoding='utf-8')

# ── CONFIGURACIÓN ────────────────────────────────────────────
API_KEY  = "xd"
MODEL    = "llama-3.3-70b-versatile"
GROQ_URL = "https://api.groq.com/openai/v1/chat/completions"

CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=(localdb)\\MSSQLLocalDB;"
    "DATABASE=WeezoDB;"
    "Trusted_Connection=yes;"
)

# ── PERSONALIDADES (6 MODOS) ─────────────────────────────────
def construir_system_prompt(nombre: str, modo: str, idioma: str) -> str:
    lengua = "español" if idioma == "es" else "English"

    personalidades = {
        "normal": f"""
Eres Weezo, un asistente de productividad neutral y directo para {nombre}.
Analizas las ventanas activas y das retroalimentación objetiva, sin exagerar.
- Programación (IDEs, código, SQL): reconoce el buen trabajo brevemente.
- Juegos: señala neutralmente que está jugando y sugiere retomar el trabajo si lleva rato.
- Redes/YouTube: recuérdale enfocarse, sin dramatismo.
TONO: Profesional, claro, conciso. Sin apodos cariñosos ni regaños emocionales.
""",
        "mommy": f"""
Eres Weezo, una figura maternal cariñosa y dulce para {nombre}, estilo "ara ara".
- Programación: felicítalo con ternura ("good boy~", "estoy orgullosa de ti").
- Juegos: regáñalo dulcemente ("ara ara~", "my little boy", recuérdale sus deberes).
- Redes/YouTube: anímalo con cariño a enfocarse ("honey", "mi niño").
TONO: Suave, cálido, maternal. Usa apodos cariñosos.
""",
        "estricto": f"""
Eres Weezo, una figura paterna estricta y exigente para {nombre}.
- Programación: aprobación firme y seca ("Bien. Eso se espera de ti.").
- Juegos: desaprobación clara y directa ("¿Otra vez jugando? Tienes responsabilidades.").
- Redes/YouTube: corrección firme ("Deja las distracciones y vuelve al trabajo.").
TONO: Serio, firme, exigente. Como un padre que espera lo mejor. Nada de apodos cariñosos.
""",
        "militar": f"""
Eres Weezo, un instructor militar para {nombre}. Te diriges a él como "soldado" o "recluta".
- Programación: aprobación con disciplina ("¡Así se hace, soldado! Esa es la actitud.").
- Juegos: reprimenda militar ("¡{nombre}! ¿Esto es lo que llamas disciplina? ¡A trabajar!").
- Redes/YouTube: orden directa ("¡Sin distracciones! ¡Vuelve a tu misión, soldado!").
TONO: Enérgico, autoritario, con órdenes. Usa signos de exclamación y vocabulario militar.
""",
        "coach": f"""
Eres Weezo, un coach motivacional entusiasta y positivo para {nombre}.
- Programación: celebra con energía ("¡Eso es, {nombre}! ¡Estás construyendo tu futuro!").
- Juegos: motiva a retomar sin juzgar ("Te mereces un descanso, pero recuerda tus metas, ¡tú puedes!").
- Redes/YouTube: reencauza con ánimo ("¡Vamos! Una pequeña pausa y de vuelta a brillar.").
TONO: Inspirador, energético, positivo. Como un coach de vida que cree en ti.
""",
        "sarcastico": f"""
Eres Weezo, un asistente sarcástico y burlón (pero no cruel) para {nombre}.
- Programación: elogio con ironía ("Wow, {nombre} programando. Alguien avise a la prensa.").
- Juegos: burla ligera ("Ah sí, porque ese rango no se va a subir solo, ¿verdad?").
- Redes/YouTube: sarcasmo ("Scrolleando otra vez. Muy productivo, sí señor.").
TONO: Ingenioso, irónico, burlón pero con humor. Nunca ofensivo de verdad.
"""
    }

    base = personalidades.get(modo, personalidades["normal"])

    # ANTES:
    reglas = f"""

REGLAS GENERALES:
- Responde SIEMPRE en {lengua}.
- Máximo 3 oraciones.
- No uses asteriscos ni markdown, solo texto plano.
- Basa tu respuesta en las ventanas reales que te paso.
"""
    return base + reglas

# DESPUÉS:
    if idioma == "en":
        regla_idioma = "CRITICAL: You MUST respond ONLY in English. Do not use any Spanish words at all."
    else:
        regla_idioma = "IMPORTANTE: Responde ÚNICAMENTE en español."

    reglas = f"""

REGLAS GENERALES:
- {regla_idioma}
- Máximo 3 oraciones.
- No uses asteriscos ni markdown, solo texto plano.
- Basa tu respuesta en las ventanas reales que te paso.
"""
    return base + reglas

# ── BASE DE DATOS ─────────────────────────────────────────────
def obtener_datos() -> tuple:
    try:
        conn   = pyodbc.connect(CONN_STR, timeout=5)
        cursor = conn.cursor()

        def get_config(clave, defecto):
            cursor.execute("SELECT Valor FROM Configuracion WHERE Clave = ?", clave)
            fila = cursor.fetchone()
            return fila[0] if fila else defecto

        nombre  = get_config("NombreUsuario", "amigo")
        modo    = get_config("ModoHabla", "normal")
        idioma  = get_config("Idioma", "es")
        api_key = get_config("ApiKey", "")

        cursor.execute("""
            SELECT TOP 7 VentanaTitulo
            FROM   LogsActividad
            WHERE  VentanaTitulo NOT LIKE '%Weezo%'
            ORDER  BY FechaHora DESC
        """)
        filas = cursor.fetchall()
        conn.close()

        if not api_key:
            print("Falta la API key. Configúrala en la aplicación.")
            sys.exit(1)

        if not filas:
            print("No hay actividad reciente registrada.")
            sys.exit(0)

        ventanas = [fila[0] for fila in filas]
        return nombre, modo, idioma, api_key, ventanas

    except pyodbc.Error as e:
        print(f"Error de base de datos: {e}")
        sys.exit(1)
# ── GROQ API (con reintentos) ────────────────────────────────
def consultar_groq(nombre, modo, idioma, api_key, ventanas) -> str:
    system_prompt = construir_system_prompt(nombre, modo, idioma)
    lista         = "\n".join(f"- {v}" for v in ventanas)
    prompt_usuario = (
        f"Estas son las últimas ventanas activas de {nombre}:\n{lista}\n\n"
        "Dame tu retroalimentación."
    )

    payload = {
        "model": MODEL,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user",   "content": prompt_usuario}
        ],
        "temperature": 0.8,
        "max_tokens":  300
    }
    headers = {
        "Content-Type":  "application/json",
        "Authorization": f"Bearer {api_key}"
    }

    # Reintenta hasta 3 veces con espera progresiva
    for intento in range(1, 4):
        try:
            response = requests.post(
                GROQ_URL, headers=headers,
                data=json.dumps(payload), timeout=20
            )

            # Si es rate limit (429) o error de servidor (5xx), reintenta
            if response.status_code == 429 or response.status_code >= 500:
                if intento < 3:
                    time.sleep(intento * 2)  # espera 2s, luego 4s
                    continue
                print("Weezo está descansando un momento, intenta de nuevo en un rato.")
                sys.exit(0)

            response.raise_for_status()
            data  = response.json()
            return data["choices"][0]["message"]["content"].strip()

        except requests.exceptions.Timeout:
            if intento < 3:
                time.sleep(intento * 2)
                continue
            print("La conexión tardó demasiado. Intenta más tarde.")
            sys.exit(0)
        except requests.exceptions.RequestException:
            if intento < 3:
                time.sleep(intento * 2)
                continue
            print("No se pudo conectar con Weezo en este momento.")
            sys.exit(0)
        except (KeyError, IndexError):
            print("Respuesta inesperada del servicio.")
            sys.exit(0)

# ── MAIN ──────────────────────────────────────────────────────
def main():
    nombre, modo, idioma, api_key, ventanas = obtener_datos()
    respuesta = consultar_groq(nombre, modo, idioma, api_key, ventanas)
    print(respuesta)

if __name__ == "__main__":
    main()