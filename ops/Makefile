# Edge Control Platform - Ops Scripts

.PHONY: up down seed logs clean

up:
	@echo "🚀 Starting Edge Control Platform..."
	docker-compose up -d
	@echo "⏳ Waiting for services to be ready..."
	@sleep 10
	@echo "🌱 Running seed script..."
	@bash seed.sh
	@echo "✅ Platform is ready!"
	@echo "🌐 Admin UI: http://localhost:3000"
	@echo "🔗 API: http://localhost:5000"

down:
	@echo "🛑 Stopping Edge Control Platform..."
	docker-compose down

seed:
	@echo "🌱 Seeding database..."
	@bash seed.sh

logs:
	@echo "📋 Showing logs..."
	docker-compose logs -f

clean:
	@echo "🧹 Cleaning up..."
	docker-compose down -v --remove-orphans
	docker system prune -f

status:
	@echo "📊 Service status:"
	@docker-compose ps

restart:
	@echo "🔄 Restarting services..."
	docker-compose restart
