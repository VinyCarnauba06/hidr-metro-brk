-- ============================================================
-- Schema Hidrômetro BRK
-- Prolar AGE — Maceió/AL
-- ============================================================

CREATE TABLE IF NOT EXISTS usuarios (
    id          SERIAL PRIMARY KEY,
    nome        VARCHAR(255) NOT NULL,
    cpf         VARCHAR(11) UNIQUE NOT NULL,
    senha_hash  VARCHAR(255) NOT NULL,
    perfil      VARCHAR(20) NOT NULL CHECK (perfil IN ('Fiscal', 'Operador', 'Admin')),
    ativo       BOOLEAN DEFAULT TRUE,
    criado_em   TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS condominios (
    id              SERIAL PRIMARY KEY,
    nome            VARCHAR(255) NOT NULL,
    endereco        VARCHAR(500),
    qtd_unidades    INT NOT NULL DEFAULT 0,
    tipo_medidor    VARCHAR(30) DEFAULT 'AguaFria'
                        CHECK (tipo_medidor IN ('Gas','AguaFria','AguaQuente','AguaQuenteEFria')),
    criado_em       TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS unidades (
    id              SERIAL PRIMARY KEY,
    condominio_id   INT NOT NULL REFERENCES condominios(id) ON DELETE RESTRICT,
    numero          VARCHAR(20) NOT NULL,
    tipo            VARCHAR(50),
    ativa           BOOLEAN DEFAULT TRUE,
    UNIQUE (condominio_id, numero)
);

CREATE TABLE IF NOT EXISTS ordens_servico (
    id              SERIAL PRIMARY KEY,
    condominio_id   INT NOT NULL REFERENCES condominios(id) ON DELETE RESTRICT,
    fiscal_id       INT REFERENCES usuarios(id) ON DELETE SET NULL,
    mes             INT NOT NULL CHECK (mes BETWEEN 1 AND 12),
    ano             INT NOT NULL CHECK (ano >= 2020),
    data_inicio     TIMESTAMP DEFAULT NOW(),
    data_conclusao  TIMESTAMP,
    status          VARCHAR(20) DEFAULT 'Aberta'
                        CHECK (status IN ('Aberta','EmProgresso','Validada','Finalizada')),
    criado_em       TIMESTAMP DEFAULT NOW(),
    UNIQUE (condominio_id, mes, ano)
);

CREATE TABLE IF NOT EXISTS leituras_hidrometro (
    id                      SERIAL PRIMARY KEY,
    os_id                   INT NOT NULL REFERENCES ordens_servico(id) ON DELETE RESTRICT,
    unidade_id              INT NOT NULL REFERENCES unidades(id) ON DELETE RESTRICT,

    foto_path               VARCHAR(500),

    valor_m3                NUMERIC(8,2),
    valor_litros            INT DEFAULT 0,
    valor_m3_validado       NUMERIC(8,2),

    origem                  VARCHAR(10) DEFAULT 'Ia' CHECK (origem IN ('Ia','Manual')),
    confianca_ia            NUMERIC(3,2),
    tentativas              INT DEFAULT 0,

    status                  VARCHAR(15) DEFAULT 'Pendente'
                                CHECK (status IN ('Pendente','Validado','Rejeitado')),
    qualidade_foto          VARCHAR(20) DEFAULT 'Ok'
                                CHECK (qualidade_foto IN ('Ok','BaixaConfianca','Manual','Rejeitado3x')),
    suspeita_vazamento      BOOLEAN DEFAULT FALSE,
    recomendacao_revisao    BOOLEAN DEFAULT FALSE,

    observacao              TEXT,
    motivo_rejeicao         VARCHAR(255),

    criado_em               TIMESTAMP DEFAULT NOW(),
    criado_por_id           INT REFERENCES usuarios(id) ON DELETE SET NULL,
    validado_por_id         INT REFERENCES usuarios(id) ON DELETE SET NULL,
    validado_em             TIMESTAMP
);

CREATE TABLE IF NOT EXISTS historico_consumo (
    id              SERIAL PRIMARY KEY,
    unidade_id      INT NOT NULL REFERENCES unidades(id) ON DELETE RESTRICT,
    os_id           INT REFERENCES ordens_servico(id) ON DELETE SET NULL,
    consumo_m3      NUMERIC(8,2),
    leitura_anterior NUMERIC(8,2),
    leitura_atual   NUMERIC(8,2),
    mes             INT,
    ano             INT,
    criado_em       TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS historico_troca_hidrometro (
    id                      SERIAL PRIMARY KEY,
    unidade_id              INT NOT NULL REFERENCES unidades(id) ON DELETE RESTRICT,
    data_troca              DATE,
    numero_serie_anterior   VARCHAR(50),
    numero_serie_novo       VARCHAR(50),
    motivo                  VARCHAR(100),
    criado_por_id           INT REFERENCES usuarios(id) ON DELETE SET NULL,
    criado_em               TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS auditoria (
    id              SERIAL PRIMARY KEY,
    usuario_id      INT REFERENCES usuarios(id) ON DELETE SET NULL,
    tabela          VARCHAR(100),
    acao            VARCHAR(50) NOT NULL,
    registro_id     INT,
    dados_antes     JSONB,
    dados_depois    JSONB,
    origem          VARCHAR(50),
    motivo          VARCHAR(255),
    criado_em       TIMESTAMP DEFAULT NOW()
);

-- Índices de performance
CREATE INDEX IF NOT EXISTS idx_leituras_os         ON leituras_hidrometro(os_id);
CREATE INDEX IF NOT EXISTS idx_leituras_unidade     ON leituras_hidrometro(unidade_id);
CREATE INDEX IF NOT EXISTS idx_leituras_status      ON leituras_hidrometro(status);
CREATE INDEX IF NOT EXISTS idx_leituras_criado_em   ON leituras_hidrometro(criado_em);
CREATE INDEX IF NOT EXISTS idx_historico_unidade    ON historico_consumo(unidade_id);
CREATE INDEX IF NOT EXISTS idx_os_condominio        ON ordens_servico(condominio_id);
CREATE INDEX IF NOT EXISTS idx_os_status            ON ordens_servico(status);
CREATE INDEX IF NOT EXISTS idx_auditoria_tabela     ON auditoria(tabela);
CREATE INDEX IF NOT EXISTS idx_auditoria_usuario    ON auditoria(usuario_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_criado_em  ON auditoria(criado_em);
