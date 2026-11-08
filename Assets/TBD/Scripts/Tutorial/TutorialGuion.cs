namespace TBD.Tutorial
{
    /// <summary>
    /// Textos del tutorial, tal cual los dejó Lucía en docs/tutorial-textos.md (D-N154, D-N163). Si cambia un texto, se cambia aquí y en ese documento.
    /// Las teclas con * eran propuestas: aquí ya va la tecla que usa el juego (si cambia la tecla, se cambia el texto, no la tecla).
    /// Capítulos 0 y 3 y la tarjeta del C90: Iker. Capítulos 1 y 2: los textos van aquí para que todo el tutorial use el mismo motor, pero las señales
    /// que los cierran las manda la zona (Marcos). La lista de señales y de resaltados está en Señales, abajo.
    /// </summary>
    public static class TutorialGuion
    {
        /// <summary>Nombres de las señales que el motor espera. La vista del mapa y la del tren mandan las del capítulo 3; la zona, las de los capítulos 1 y 2.</summary>
        public static class Senales
        {
            public const string Empezar = "boton:Empezar";
            // capítulo 1 (zona, Marcos)
            public const string CamaraMovida = "camara_movida", RuedaUsada = "rueda_usada", PelotonSeleccionado = "peloton_seleccionado", OrdenMover = "orden_mover",
                FormacionDada = "formacion_dada", PausaCerrada = "pausa_cerrada", HordaAvanza = "horda_avanza", AtaqueRechazado = "ataque_rechazado", VehiculosFuera = "vehiculos_fuera";
            // capítulo 2 (zona, Marcos)
            public const string ObrasAbierto = "obras_abierto", TramoSeleccionado = "tramo_seleccionado", ReparacionConfirmada = "reparacion_confirmada",
                TramoSobre70 = "tramo_sobre_70", ObrasCerrado = "obras_cerrado";
            // capítulo 3 (mapa y panel del tren, Iker)
            public const string MapaAbierto = "mapa_abierto", PanelTrenAbierto = "panel_tren_abierto", CargaPuesta = "carga_puesta", PelotonABordo = "peloton_a_bordo",
                VehiculoABordo = "vehiculo_a_bordo", SinApoyo = "sin_apoyo", DestinoNules = "destino_nules", TrenEnviado = "tren_enviado", TrenEnNules = "tren_en_nules",
                PanelNulesAbierto = "panel_nules_abierto", Descargado = "descargado", VueltaZona = "vuelta_zona";
            // tarjeta suelta del C90 (zona: C90Aviso.Buscar)
            public const string C90ATiro = "c90_a_tiro", C90Lanzado = "c90_lanzado";
        }

        /// <summary>Claves de lo que se resalta: quien dibuja ese elemento registra su rectángulo en pantalla con esa clave.</summary>
        public static class Resaltes
        {
            public const string BarraAtajos = "barra_atajos", GrupoZombis = "grupo_zombis", Puertas = "puertas", TarjetasFuera = "tarjetas_peloton_fuera", TarjetasPeloton = "tarjetas_peloton",
                TramoOeste = "tramo_oeste", TeclasFormacion = "teclas_f1_f4", BotonMenu = "boton_menu", BarraMuro = "barra_muro",
                TramoDanado = "tramo_danado", BotonObras = "boton_obras", CosteObra = "coste_obra", Zapadores = "zapadores", IconoRuido = "icono_ruido",
                NodoSagunto = "nodo_sagunto", NodoNules = "nodo_nules", NodoCastellon = "nodo_castellon", BotonPrepararTren = "boton_preparar_tren",
                Cargas = "cargas_tren", ListaUnidades = "lista_unidades", ListaVehiculos = "lista_vehiculos", Eta = "eta_tren", BotonEnviar = "boton_enviar",
                EstadoNules = "estado_nules", BotonDescargar = "boton_descargar";
        }

        static Paso S(string id, string texto, string[] resalta, params string[] senales) { return new Paso { Id = id, Texto = texto, Resalta = resalta ?? new string[0], Cierre = Cierre.Senal, Senales = senales }; }
        static Paso T(string id, string texto, float s, params string[] resalta) { return new Paso { Id = id, Texto = texto, Resalta = resalta, Cierre = Cierre.Segundos, Segundos = s }; }
        static string[] R(params string[] k) { return k; }

        public static Capitulo Cap0()
        {
            var p = S("0.1", "Sagunto es tu base. Aquí empieza todo.", null, Senales.Empezar);
            p.Pausa = true; p.Botones = new[] { "Empezar", TutorialMotor.BotonSaltar };
            return new Capitulo { Numero = 0, Titulo = "Intro", Pasos = new[] { p } };
        }

        public static Capitulo Cap1()
        {
            var p13 = T("1.3", "Tienes tropa dentro del muro y tropa fuera, junto a los vehículos. La de fuera es la primera que recibe.", 4, Resaltes.TarjetasFuera);
            p13.ExtraSi = Senales.VehiculosFuera; p13.Extra = "Los vehículos también disparan. Selecciónalos igual que a la tropa.";
            var p11 = S("1.1", "Mueve la cámara con W A S D. Acércate y aléjate con la rueda.", R(Resaltes.BarraAtajos), Senales.CamaraMovida, Senales.RuedaUsada);
            p11.Todas = true;
            var p19 = S("1.9", "Vigila la vida del muro arriba. Si un tramo cae, entran.", R(Resaltes.BarraMuro), Senales.AtaqueRechazado); p19.Linea = true; p19.Segundos = 90f;   // red de seguridad (Nacho)
            var p18 = S("1.8", "Ahí vienen. Disparan solos cuando los tienen a tiro.", null, Senales.HordaAvanza); p18.Segundos = 3f; p18.SueltaHorda = true;   // aquí se suelta la horda; se cierra al avanzar o a los 3 s
            return new Capitulo
            {
                Numero = 1, Titulo = "Defender Sagunto de día", Combate = true,
                Pasos = new[]
                {
                    p11,
                    T("1.2", "Se acerca un grupo desde el oeste. Las puertas se han cerrado: para entrar tendrán que romper el muro.", 4, Resaltes.GrupoZombis, Resaltes.Puertas),
                    p13,
                    S("1.4", "Selecciona un pelotón: clic en él, en su tarjeta o con su número (1 a 5). Para coger varios, arrastra un recuadro.", R(Resaltes.TarjetasPeloton), Senales.PelotonSeleccionado),
                    S("1.5", "Clic derecho para moverlo. Ponlo junto al muro oeste.", R(Resaltes.TramoOeste), Senales.OrdenMover),
                    S("1.6", "Arrastra con el clic derecho para dibujar un frente. Prueba una formación con F1 a F4.", R(Resaltes.TeclasFormacion), Senales.FormacionDada),
                    S("1.7", "Esc abre el menú de pausa y congela la partida. Desde ahí también se salta el tutorial.", R(Resaltes.BotonMenu), Senales.PausaCerrada),
                    p18,
                    p19,
                    T("1.10", "Ataque rechazado. Esta noche vendrán muchos más.", 4),
                }
            };
        }

        public static Capitulo Cap2()
        {
            return new Capitulo
            {
                Numero = 2, Titulo = "Reparar",
                Pasos = new[]
                {
                    T("2.1", "El muro oeste está tocado. Esta noche no aguantará así.", 4, Resaltes.TramoDanado),
                    S("2.2", "Pulsa B para entrar en Obras. El combate sigue mientras construyes.", R(Resaltes.BotonObras), Senales.ObrasAbierto),
                    S("2.3", "Los tramos dañados salen marcados. Haz clic en el del oeste.", R(Resaltes.TramoDanado), Senales.TramoSeleccionado),
                    S("2.4", "Reparar cuesta materiales y tiempo. Confirma con clic.", R(Resaltes.CosteObra), Senales.ReparacionConfirmada),
                    S("2.5", "Van los zapadores. Mientras reparan no disparan, y el ruido atrae a la horda.", R(Resaltes.Zapadores, Resaltes.IconoRuido), Senales.TramoSobre70),
                    T("2.6", "Si un tramo cae del todo, se reconstruye, y solo con la zona limpia. Sale caro.", 4),
                    S("2.7", "Sal de Obras con B o con clic derecho en vacío.", R(Resaltes.BotonObras), Senales.ObrasCerrado),
                }
            };
        }

        public static Capitulo Cap3()
        {
            var p311 = S("3.11", "El tren ha llegado a Nules. Haz clic en Nules para descargar.", R(Resaltes.NodoNules), Senales.PanelNulesAbierto);
            p311.Espera = Senales.TrenEnNules;     // «Al llegar»: no sale hasta que el tren está en Nules
            var p310 = T("3.10", "Si una estación está sitiada, el tren espera a las puertas y fuera le pueden atacar. Mira el estado antes de mandar nada.", 5, Resaltes.EstadoNules);
            return new Capitulo
            {
                Numero = 3, Titulo = "Llevar un tren a Nules",
                Pasos = new[]
                {
                    T("3.1", "Nules está aislada y le falta de todo. Hay que llevarle un tren.", 4),
                    S("3.2", "Aleja la cámara del todo para salir al mapa, o pulsa M. Sagunto sigue aquí mientras estás fuera.", R(Resaltes.BarraAtajos), Senales.MapaAbierto),
                    T("3.3", "Estas son tus 3 estaciones. Castellón está infestada; de momento, ni te acerques.", 4, Resaltes.NodoSagunto, Resaltes.NodoNules, Resaltes.NodoCastellon),
                    S("3.4", "Haz clic en Sagunto y abre Preparar tren.", R(Resaltes.NodoSagunto, Resaltes.BotonPrepararTren), Senales.PanelTrenAbierto),
                    S("3.5", "Elige la carga. A Nules le hacen falta munición y materiales.", R(Resaltes.Cargas), Senales.CargaPuesta),
                    S("3.6", "Sube un pelotón. Viajará en el tren y se quedará donde lo bajes.", R(Resaltes.ListaUnidades), Senales.PelotonABordo),
                    S("3.7", "Puedes mandar apoyo terrestre con el tren: un vehículo en una plataforma.", R(Resaltes.ListaVehiculos), Senales.VehiculoABordo, Senales.SinApoyo),
                    S("3.8", "Destino: Nules. Mira el ETA antes de mandarlo.", R(Resaltes.NodoNules, Resaltes.Eta), Senales.DestinoNules),
                    S("3.9", "Envíalo.", R(Resaltes.BotonEnviar), Senales.TrenEnviado),
                    p310,
                    p311,
                    S("3.12", "Descarga la carga y la tropa.", R(Resaltes.BotonDescargar), Senales.Descargado),
                    S("3.13", "Nules ya tiene con qué aguantar. Vuelve a Sagunto: cae la noche.", R(Resaltes.NodoSagunto), Senales.VueltaZona),
                }
            };
        }

        public static Suelta C90()
        {
            return new Suelta { Id = "c90", Texto = "Tus fusileros llevan C90. Tecla C para lanzarlo. Se repone al recargar.", Disparo = Senales.C90ATiro, Cierra = new[] { Senales.C90Lanzado }, Segundos = 10f };
        }

        public static Capitulo[] Todos() { return new[] { Cap0(), Cap1(), Cap2(), Cap3() }; }

        public static TutorialMotor Nuevo(IMemoria mem) { return new TutorialMotor(Todos(), new[] { C90() }, mem); }
    }
}
