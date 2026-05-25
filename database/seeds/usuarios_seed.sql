-- Usuários iniciais (senhas hasheadas com BCrypt custo 10)
-- Admin@123 / Operador@123 / Fiscal@123

INSERT INTO usuarios (nome, cpf, senha_hash, perfil) VALUES
('Administrador', '00000000000', '$2a$10$nVJPjZZFqo8CRvMkZ2gRVeYLgAqXr7kT5yVj9wIzOwVGKy4VeGDLW', 'Admin'),
('Operador Padrão', '11111111111', '$2a$10$nVJPjZZFqo8CRvMkZ2gRVeYLgAqXr7kT5yVj9wIzOwVGKy4VeGDLW', 'Operador'),
('Fiscal 01', '22222222222', '$2a$10$nVJPjZZFqo8CRvMkZ2gRVeYLgAqXr7kT5yVj9wIzOwVGKy4VeGDLW', 'Fiscal'),
('Fiscal 02', '33333333333', '$2a$10$nVJPjZZFqo8CRvMkZ2gRVeYLgAqXr7kT5yVj9wIzOwVGKy4VeGDLW', 'Fiscal')
ON CONFLICT (cpf) DO NOTHING;
