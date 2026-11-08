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
                PanelNulesAbierto = "panel_nules_abierto", Descargado = "descargado", VueltaZona = "vuelta_zona",
                // D-N172 (capítulo 3: refuerzos de Nules)
                MaterialesPuestos = "materiales_puestos", SinCarga = "sin_carga", DestinoSagunto = "destino_sagunto", TrenEnSagunto = "tren_en_sagunto",
                SaguntoSitiada = "sagunto_sitiada", RefuerzosEnTierra = "refuerzos_en_tierra", TropaEnTierra = "tropa_en_tierra", LinceABordo = "lince_a_bordo",
                Pila150 = "pila_150", OrdenAtacar = "orden_atacar",
                ContactoMuro = "contacto_muro",
                // capítulo 4 (aeródromo): el panel de Bétera manda las tres primeras; la zona (Marcos), posado y descargado
                AerodromoAbierto = "aerodromo_abierto", NH90ConPeloton = "nh90_con_peloton", NH90Enviado = "nh90_enviado", NH90Llega = "nh90_llega",
                NH90Posado = "nh90_posado", NH90Descargado = "nh90_descargado",                 // la zona: el primer zombi toca el muro
                Cap3Dispara = "cap3_dispara", Cap3PorTiempo = "cap3_por_tiempo";   // estados de la vista: pila > 150, o 60 s reales desde el primer contacto   // la zona manda pila_150 la primera vez que la pila pasa de 150 (D-N168); se recuerda como estado
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
                EstadoNules = "estado_nules", BotonDescargar = "boton_descargar",
                AvisoRadar = "aviso_radar", ManchaHorda = "mancha_horda", TarjetasDestacamento = "tarjetas_destacamento", EstadoSagunto = "estado_sagunto", TrenAnden = "tren_anden",
                Pila = "pila", AvisoPila = "aviso_pila",
                NodoBetera = "nodo_betera", TarjetasNH90 = "tarjetas_nh90", ConPeloton = "con_peloton", FlechaBorde = "flecha_borde";
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

        /// <summary>
        /// Capítulo 3, «Pedir refuerzos a otra estación» (D-N172 revisada tras medirla Hugo y Nacho): salta en la primera noche, la primera vez que la pila
        /// pasa de 150 (D-N168), y la horda NO espera. Nules manda 1 pelotón de fusileros (sin Lince); el tren llega a las puertas de Sagunto sitiada y la
        /// tropa baja fuera (D-N166). «{eta}» lo rellena la vista.
        /// </summary>
        public static Capitulo Cap3()
        {
            var p31 = T("3.1", "¡Están trepando el muro! Sagunto sola no aguantará. Nules, la estación de al lado, puede mandarte ayuda.", 4, Resaltes.Pila, Resaltes.AvisoPila);
            var p31b = T("3.1b", "La horda está en el muro. Sagunto sola no aguantará mucho. Nules, la estación de al lado, puede mandarte ayuda.", 4, Resaltes.Pila, Resaltes.AvisoPila);
            p31.Espera = p31b.Espera = Senales.Cap3Dispara;     // pila > 150 o 60 s desde el primer contacto, lo que ocurra antes (Lucía)
            p31.AltSi = Senales.Cap3PorTiempo; p31.Alt = p31b;  // si salta por tiempo, el 3.1b
            var p37 = S("3.7", "Sagunto está sitiada: el tren espera a las puertas. Baja la tropa: atacará a la horda por la espalda.", R(Resaltes.TrenAnden, Resaltes.BotonDescargar), Senales.TropaEnTierra, Senales.RefuerzosEnTierra);
            p37.Espera = Senales.TrenEnSagunto;     // «Al llegar el tren»
            return new Capitulo
            {
                Numero = 3, Titulo = "Pedir refuerzos a otra estación",
                Pasos = new[]
                {
                    p31,
                    S("3.2", "Sal al mapa: aleja la cámara del todo o pulsa M. El combate sigue mientras estás fuera.", R(Resaltes.BarraAtajos), Senales.MapaAbierto),
                    S("3.3", "Haz clic en Nules. Castellón está infestada; de momento, ni te acerques.", R(Resaltes.NodoSagunto, Resaltes.NodoNules, Resaltes.NodoCastellon), Senales.PanelNulesAbierto),
                    S("3.4", "Nules tiene un tren en el andén y un pelotón de fusileros. Abre Preparar tren y sube el pelotón.", R(Resaltes.TarjetasDestacamento, Resaltes.BotonPrepararTren, Resaltes.ListaUnidades), Senales.PelotonABordo),
                    S("3.5", "Destino: Sagunto. Mira el ETA y envíalo.", R(Resaltes.NodoSagunto, Resaltes.Eta, Resaltes.BotonEnviar), Senales.TrenEnviado),
                    S("3.6", "Vuelve a Sagunto y aguanta: doble clic en Sagunto o acerca la cámara. La ayuda llega en {eta}.", R(Resaltes.NodoSagunto), Senales.VueltaZona),
                    p37,
                    S("3.8", "Llévalos contra la pila del oeste.", R(Resaltes.Pila), Senales.OrdenMover, Senales.OrdenAtacar),
                }
            };
        }

        /// <summary>Después de la primera noche (D-N172): objetivo sin tarjetas de tutorial (el viaje con carga de la D-N154).</summary>
        public const string ObjetivoDevolverTren = "Lleva el tren a Nules con munición";

        /// <summary>
        /// Capítulo 4, apoyo aéreo desde Bétera (D-N173; adapta tutorial-refuerzos.md con el aeródromo como origen). Salta la primera vez que el jugador abre el
        /// aeródromo, con las reglas de siempre (fácil y normal, una vez). Textos aprobados por Lucía (8-nov 15:10, tutorial-cap4-iker.md). L y U son teclas propuestas: si cambian, se cambia el texto.
        /// </summary>
        public static Capitulo Cap4()
        {
            var p45 = T("4.5", "El NH90 entra por el borde. Sigue la flecha.", 5, Resaltes.FlechaBorde);
            p45.Espera = Senales.NH90Llega;
            return new Capitulo
            {
                Numero = 4, Titulo = "Apoyo aéreo desde Bétera",
                Pasos = new[]
                {
                    T("4.1", "Base aérea de Bétera. Desde aquí operan los helicópteros: dos NH90 y pelotones de fusileros para embarcar.", 5, Resaltes.TarjetasNH90),
                    S("4.2", "Elige un NH90 y sube un pelotón.", R(Resaltes.TarjetasNH90, Resaltes.ConPeloton), Senales.NH90ConPeloton),
                    S("4.3", "Destino: Sagunto. Mira el ETA y envíalo.", R(Resaltes.NodoSagunto, Resaltes.Eta, Resaltes.BotonEnviar), Senales.NH90Enviado),
                    S("4.4", "Vuelve a Sagunto: doble clic en Sagunto o acerca la cámara.", R(Resaltes.NodoSagunto), Senales.VueltaZona),
                    p45,
                    S("4.6", "Pósalo en un claro: tecla L. El cursor marca dónde cabe.", R(Resaltes.TarjetasNH90), Senales.NH90Posado),
                    S("4.7", "Baja la tropa: tecla U.", R(Resaltes.TarjetasNH90), Senales.NH90Descargado),
                    T("4.8", "Cada salida gasta combustible de Bétera. Con poca munición vuelve solo a rearmar, y mientras tanto no lo tienes.", 6),
                }
            };
        }
        public const string ClaveCap4 = "tutorial4.visto";
        public static TutorialMotor NuevoCap4(IMemoria mem) { return new TutorialMotor(new[] { Cap4() }, null, mem, ClaveCap4); }

        public static Suelta C90()
        {
            return new Suelta { Id = "c90", Texto = "Tus fusileros llevan C90. Tecla C para lanzarlo. Se repone al recargar.", Disparo = Senales.C90ATiro, Cierra = new[] { Senales.C90Lanzado }, Segundos = 10f };
        }

        public static Capitulo[] Todos() { return new[] { Cap0(), Cap1(), Cap2(), Cap3() }; }

        public static TutorialMotor Nuevo(IMemoria mem) { return new TutorialMotor(Todos(), new[] { C90() }, mem); }
    }
}
