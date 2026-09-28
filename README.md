# Screen Time Tracker

Aplicación de escritorio para Windows desarrollada con C#, .NET 8 y WPF, diseñada para monitorear y registrar el tiempo de uso en pantalla de manera automática, categorizando la actividad y almacenando los datos de forma local.

## Descripción del Proyecto

Screen Time Tracker monitorea la ventana activa en primer plano a intervalos regulares con un consumo mínimo de recursos. El sistema clasifica el tiempo de uso en tres categorías principales (Juegos, Escritorio y Web), permitiendo consultar estadísticas diarias y semanales a través de una interfaz gráfica en modo oscuro.

## Funciones Principales

- **Monitoreo de actividad en tiempo real**: Detecta la ventana y el proceso activo en primer plano mediante llamadas nativas a la API de Windows (`user32.dll`).
- **Detección inteligente de juegos**: Identifica videojuegos a partir de las rutas de bibliotecas configuradas (Steam, Epic Games, Battle.net, entre otras) o carpetas personalizadas, reconociendo títulos incluso en modo ventana sin bordes (*Borderless Windowed*) y omitiendo los lanzadores.
- **Rastreo web**: Identifica dominios y pestañas activas en navegadores compatibles mediante *UI Automation*.
- **Categorización automática**: Organiza el tiempo en tiempo total, juegos, aplicaciones de escritorio y navegación web.
- **Historial y estadísticas**:
  - Vista diaria detallada con desglose por aplicación o sitio.
  - Navegación entre fechas para consultar registros históricos.
  - Gráfico semanal interactivo (de lunes a domingo) para visualizar tendencias.
- **Persistencia local en JSON**: Guarda los registros diarios y la configuración en la carpeta `registros/` en formato JSON, garantizando privacidad total sin dependencias de servicios externos ni telemetría.
- **Configuración personalizada**: Permite gestionar las rutas de bibliotecas de juegos y ajustar los parámetros de monitoreo desde la interfaz o editando el archivo de configuración.

## Requisitos del Sistema

- Sistema operativo: Windows 10 o Windows 11.
- Para desarrollo y compilación: SDK de .NET 8.0 o superior.

## Instrucciones de Uso

### Ejecutar en modo desarrollo
```bash
dotnet run
```

### Ejecutar con recarga en vivo (Hot Reload)
```bash
dotnet watch
```

### Compilar el proyecto
```bash
dotnet build
```

### Publicar ejecutable portable independiente
Para generar un binario único que no requiera tener .NET instalado en el equipo de destino:
```powershell
dotnet publish -c Release -o "./publish/portable"
```
El archivo ejecutable se creará en `publish/portable/ScreenTimeTracker.exe`.

## Estructura del Proyecto

- **Models**: Clases de datos para registros de actividad, acumulados diarios y configuración.
- **Services**: Servicios de captura de ventanas (`NativeMethods`), extracción web (`EdgeAutomationService`), almacenamiento local (`StorageService`) y ciclo de monitoreo (`WindowTrackerService`).
- **ViewModels**: Lógica de presentación y comandos bajo el patrón MVVM.
- **Assets**: Recursos visuales e iconos de la aplicación.
- **registros**: Directorio local donde se almacenan las configuraciones y los archivos JSON diarios.

## Licencia

Este proyecto está bajo la Licencia Pública General de GNU v3.0 (GNU GPLv3). Consulta el archivo [LICENSE](file:///c:/Users/Marcos/Desktop/Time%20Screen/LICENSE) para más detalles.

