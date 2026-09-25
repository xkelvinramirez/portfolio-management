"use client";

import { type FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { GuestGuard } from "@/components/layout/GuestGuard";
import { BrandMark } from "@/components/ui/BrandMark";
import { Button } from "@/components/ui/Button";
import { TextField } from "@/components/ui/TextField";
import { useRegister } from "@/hooks/useAuth";

export default function RegisterPage() {
  return (
    <GuestGuard>
      <RegisterForm />
    </GuestGuard>
  );
}

function RegisterForm() {
  const [email, setEmail] = useState("");
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [password, setPassword] = useState("");
  const [done, setDone] = useState(false);
  const registerMutation = useRegister();
  const router = useRouter();

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    registerMutation.mutate(
      { email, firstName, lastName: lastName.trim() || undefined, password },
      { onSuccess: () => setDone(true) }
    );
  }

  if (done) {
    return (
      <div className="flex min-h-full flex-1 items-center justify-center px-4 py-16">
        <div className="w-full max-w-sm border border-rule bg-paper p-6 text-center">
          <BrandMark size={28} className="mx-auto" />
          <h1 className="mt-3 text-lg font-semibold tracking-tight">Cuenta creada</h1>
          <p className="mt-2 text-sm text-ink-muted">Ya puedes iniciar sesión.</p>
          <Button className="mt-4 w-full justify-center" onClick={() => router.push("/login")}>
            Ir a iniciar sesión
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-full flex-1 items-center justify-center px-4 py-16">
      <div className="w-full max-w-sm border border-rule bg-paper p-6">
        <BrandMark size={28} />
        <h1 className="mt-3 text-lg font-semibold tracking-tight">Crear cuenta</h1>
        <p className="mt-1 text-sm text-ink-muted">Registra tu propio ledger de portfolio.</p>

        <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-4">
          <TextField
            label="Nombre"
            name="firstName"
            autoComplete="given-name"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            required
          />
          <TextField
            label="Apellido (opcional)"
            name="lastName"
            autoComplete="family-name"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
          />
          <TextField
            label="Email"
            name="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          <TextField
            label="Contraseña"
            name="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
          />

          {registerMutation.isError && (
            <p role="alert" className="text-sm text-accent">
              No se pudo crear la cuenta. ¿Quizás ese email ya está registrado?
            </p>
          )}

          <Button type="submit" disabled={registerMutation.isPending} className="justify-center">
            {registerMutation.isPending ? "Creando…" : "Crear cuenta"}
          </Button>
        </form>

        <p className="mt-4 text-center text-sm text-ink-muted">
          ¿Ya tienes cuenta?{" "}
          <Link href="/login" className="underline">
            Inicia sesión
          </Link>
        </p>
      </div>
    </div>
  );
}
