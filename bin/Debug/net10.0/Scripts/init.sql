-- ============================================================
--  CAJERO AUTOMÁTICO - Script de inicialización de base de datos
--  Base de datos: cajero.db (SQLite)
-- ============================================================

PRAGMA foreign_keys = ON;

-- ============================================================
-- TABLA: Usuarios
-- Almacena los titulares de las tarjetas
-- ============================================================
CREATE TABLE IF NOT EXISTS Usuarios (
    id_usuario      INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre_titular  TEXT    NOT NULL,
    numero_tarjeta  TEXT    NOT NULL UNIQUE,
    pin             TEXT    NOT NULL,          -- Guardado como hash SHA-256
    saldo           REAL    NOT NULL DEFAULT 0.00,
    intentos_fallidos INTEGER NOT NULL DEFAULT 0,
    bloqueado       INTEGER NOT NULL DEFAULT 0, -- 0 = activo, 1 = bloqueado
    fecha_creacion  TEXT    NOT NULL DEFAULT (datetime('now')),
    fecha_actualizacion TEXT NOT NULL DEFAULT (datetime('now'))
);

-- ============================================================
-- TABLA: Transacciones
-- Registro de todas las operaciones realizadas
-- ============================================================
CREATE TABLE IF NOT EXISTS Transacciones (
    id_transaccion  INTEGER PRIMARY KEY AUTOINCREMENT,
    id_usuario      INTEGER NOT NULL,
    tipo_operacion  TEXT    NOT NULL,   -- 'RETIRO', 'DEPOSITO', 'CONSULTA'
    monto           REAL    NOT NULL DEFAULT 0.00,
    saldo_anterior  REAL    NOT NULL,
    saldo_posterior REAL    NOT NULL,
    descripcion     TEXT,
    fecha           TEXT    NOT NULL DEFAULT (datetime('now')),
    exitosa         INTEGER NOT NULL DEFAULT 1, -- 1 = éxito, 0 = fallida
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
);

-- ============================================================
-- TABLA: SesionesAuditoria
-- Auditoría de intentos de acceso (correctos e incorrectos)
-- ============================================================
CREATE TABLE IF NOT EXISTS SesionesAuditoria (
    id_sesion       INTEGER PRIMARY KEY AUTOINCREMENT,
    numero_tarjeta  TEXT    NOT NULL,
    exitosa         INTEGER NOT NULL DEFAULT 0, -- 1 = éxito, 0 = fallida
    ip_origen       TEXT,
    fecha           TEXT    NOT NULL DEFAULT (datetime('now'))
);

-- ============================================================
-- ÍNDICES para optimizar consultas frecuentes
-- ============================================================
CREATE INDEX IF NOT EXISTS idx_usuarios_tarjeta     ON Usuarios(numero_tarjeta);
CREATE INDEX IF NOT EXISTS idx_transacciones_usuario ON Transacciones(id_usuario);
CREATE INDEX IF NOT EXISTS idx_transacciones_fecha   ON Transacciones(fecha);
CREATE INDEX IF NOT EXISTS idx_auditoria_tarjeta     ON SesionesAuditoria(numero_tarjeta);

-- ============================================================
-- TRIGGERS
-- ============================================================

-- Actualiza fecha_actualizacion al modificar un usuario
CREATE TRIGGER IF NOT EXISTS trg_usuarios_update
AFTER UPDATE ON Usuarios
BEGIN
    UPDATE Usuarios SET fecha_actualizacion = datetime('now')
    WHERE id_usuario = NEW.id_usuario;
END;

-- Bloquea la cuenta si los intentos fallidos llegan a 3
CREATE TRIGGER IF NOT EXISTS trg_bloquear_cuenta
AFTER UPDATE OF intentos_fallidos ON Usuarios
WHEN NEW.intentos_fallidos >= 3
BEGIN
    UPDATE Usuarios SET bloqueado = 1
    WHERE id_usuario = NEW.id_usuario;
END;

-- ============================================================
-- DATOS DE PRUEBA (tarjetas ficticias para desarrollo)
-- PIN se almacena como hash SHA-256. 
-- Valores de prueba (PIN '1234' → hash real se calcula en C#)
-- Por ahora se guardan como texto plano para pruebas iniciales.
-- ============================================================
INSERT OR IGNORE INTO Usuarios (nombre_titular, numero_tarjeta, pin, saldo)
VALUES
    ('Juan Carlos Pérez',    '4111111111111111', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', 1500.00),
    ('María López García',   '5500005555555559', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', 3200.50),
    ('Carlos Eduardo Ruiz',  '3400000000000009', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', 800.00),
    ('Ana Ruth Sanchez',     '1234567890123456', '0ffe1abd1a08215353c233d6e009613e95eec4253832a761af28ff37ac5a150c', 1000000.00), -- PIN: 1111
    ('Nayib Bukele',         '9876543210987654', 'c1f330d0aff31c1c87403f1e4347bcc21aff7c179908723535f2b31723702525', 11000000.00); -- PIN: 5555

-- Nota: hash '03ac67...' = PIN "1234" | hash 'ef92b7...' = PIN "5678"
-- Nota: hash '0ffe1a...' = PIN "1111" | hash 'c1f330...' = PIN "5555"
