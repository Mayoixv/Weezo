# ⚡ Weezo

> Tu asistente de productividad con IA que analiza lo que haces en tu PC y te da retroalimentación en tiempo real.

Weezo corre en segundo plano en Windows, rastrea la ventana activa, y cada cierto tiempo usa Inteligencia Artificial (Groq / Llama 3.3) para evaluar tu productividad y mandarte una notificación con su opinión — desde cariñosa hasta militar, tú eliges el estilo.

## ✨ Características

- 👁️ **Rastreo en tiempo real** de la ventana activa vía Win32 API
- 🤖 **IA con personalidad** — 6 modos: Normal, Mommy, Estricto, Militar, Coach, Sarcástico
- 🌐 **Bilingüe** — Español e Inglés
- 📊 **Dashboard** con gráfico de dona de tu actividad por categorías
- 📅 **Historial semanal** y **top de aplicaciones** más usadas
- 🎯 **Metas diarias** de programación con barra de progreso
- 🔔 **Notificaciones** desde la bandeja del sistema
- ⚙️ **Configurable** — nombre, idioma, modo, intervalos, no-molestar, arranque automático

## 🛠️ Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| Interfaz | C# / .NET 10 / WinForms |
| Cerebro IA | Python 3 (`cerebro.py`) |
| Base de datos | Microsoft SQL Server LocalDB |
| Modelo IA | Groq API (Llama 3.3 70B) |
| Empaquetado | PyInstaller + Inno Setup |

## 🏗️ Arquitectura



El frontend en C# rastrea la ventana activa cada segundo y la guarda en SQL Server. Periódicamente ejecuta `cerebro.py` (compilado a `.exe`), que lee las últimas ventanas, consulta a la IA según el modo configurado, y devuelve la respuesta que se muestra como notificación.

## 📦 Instalación (usuarios)

1. Descarga el instalador desde la sección [Releases](../../releases)
2. Instala **SQL Server LocalDB** si no lo tienes ([descarga aquí](https://www.microsoft.com/en-us/sql-server/sql-server-downloads))
3. Ejecuta `Weezo_Setup.exe` y sigue los pasos
4. Al abrir, la app te pedirá tu API key de Groq (gratis en [console.groq.com](https://console.groq.com/keys))

## 🚀 Desarrollo (correr el código)

1. Clona el repo
```bash
   git clone https://github.com/Mayoixv/Weezo.git
```
2. Abre la solución en Visual Studio 2022+
3. Instala dependencias de Python
```bash
   pip install pyodbc requests
```
4. Compila y ejecuta. La base de datos se crea automáticamente la primera vez.

## 👤 Autor

**Starlyn** — Estudiante de Tecnologías de la Información en el ITLA (Instituto Tecnológico de Las Américas)

---

*Un experimento sobre IA, gamificación y la pregunta: ¿qué pasaría si tu IDE tuviera sentimientos?*
