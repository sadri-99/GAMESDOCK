# Fichas con banner y actividad de Steam

En **Ajustes → Comportamiento**, activa **Ficha del juego al pasar el ratón**.

- Banner con recorte real en las dos esquinas superiores. Las esquinas inferiores del banner permanecen rectas para unirlo al texto.
- Márgenes interiores de 16 unidades y separación entre título, metadatos, actividad, descripción y pie. Texto ajustado por palabras.
- **Ficha un 15 % más grande** amplía toda la ficha al 115 %, incluido banner, texto, márgenes y curvas. Desactivado por defecto.
- **Mostrar mi actividad de Steam** añade un bloque con horas jugadas y última partida. Desactivado por defecto. Ambas preferencias se guardan y están traducidas al inglés.

## Datos personales

Se leen los registros locales de Steam de la cuenta activa. Con Steam cerrado se utiliza únicamente la cuenta marcada como más reciente; si la cuenta resulta ambigua o faltan registros, se muestra que no hay datos locales. Se identifica cada juego por su AppID exacto, nunca por una coincidencia de nombre.

El tiempo se presenta en minutos para partidas inferiores a una hora y en horas para el resto. La última partida muestra fecha y hora locales. Si Steam conserva un valor de las últimas dos semanas, solo aparece con una instantánea reciente y una partida en ese período.

Estos datos pueden ir por detrás de Steam hasta que su cliente actualice los archivos. No se inventan ceros cuando falta información, ni se muestran logros o porcentajes que el registro local no aporta. Las aperturas desde GamesDock se identifican por separado: no equivalen a partidas ni a horas jugadas en Steam.

No hace falta otra clave API ni iniciar sesión desde GamesDock. Los datos personales no se consultan por Internet ni se añaden al archivo de configuración o al registro de diagnóstico. La ficha pública y su arte conservan las consultas de Steam/SteamGridDB existentes.

## Rendimiento

Lectura en segundo plano al pasar el cursor por un juego con la opción activada. Sin sondeo continuo. Un solo lector simultáneo, cancelación al salir o cambiar de icono y caché temporal compartida, invalidada al cambiar el archivo o desactivar la opción. Solo se retienen los tres campos de actividad seleccionados. El recorte del banner se calcula al cambiar su tamaño, sin trabajo por fotograma.

## Comprobación visual

Las capturas adjuntas usan un juego, arte y actividad **sintéticos** para enseñar la composición sin compartir datos personales reales.
