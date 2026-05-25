-- Condomínios de exemplo
INSERT INTO condominios (nome, endereco, qtd_unidades, tipo_medidor) VALUES
('Residencial Praia Norte', 'Av. Beira Mar, 1500 - Maceió/AL', 40, 'AguaFria'),
('Edifício Central Park', 'Rua Dom Pedro II, 200 - Maceió/AL', 80, 'AguaFria'),
('Condomínio Jardins', 'Av. Fernandes Lima, 350 - Maceió/AL', 120, 'Gas'),
('Residencial Vila Nova', 'Rua José de Alencar, 45 - Maceió/AL', 30, 'AguaQuenteEFria');

-- Unidades do Residencial Praia Norte (40 unidades)
DO $$
BEGIN
  FOR i IN 1..40 LOOP
    INSERT INTO unidades (condominio_id, numero, tipo) VALUES
    (1, LPAD(i::text, 3, '0'), 'Apartamento')
    ON CONFLICT DO NOTHING;
  END LOOP;
END $$;

-- Unidades do Edifício Central Park (80 unidades)
DO $$
BEGIN
  FOR i IN 1..80 LOOP
    INSERT INTO unidades (condominio_id, numero, tipo) VALUES
    (2, LPAD(i::text, 3, '0'), 'Apartamento')
    ON CONFLICT DO NOTHING;
  END LOOP;
END $$;
