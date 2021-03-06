﻿namespace FsLocalState

module Eff =

    // -------
    // Kleisli
    // -------

    let kleisli (g: Eff<'b, 'c, _, _>) (f: Eff<'a, 'b, _, _>): Eff<'a, 'c, _, _> =
        fun x ->
            gen {
                let! f' = f x
                return! g f' }

    
module Gen =
    
    let run = Core.run
    
    let bind = Core.bind

    /// Return function.
    let ret = Core.ret

    /// Lifts a generator function to an effect function.    
    let toEff (gen: Gen<'s, 'r, 'o>): Eff<unit, 's, 'r, 'o> =
        fun () -> gen

    
    // ----------
    // Arithmetik
    // ----------

    let inline binOpBoth left right f = Core.binOpBoth left right f
    let inline binOpLeft left right f = Core.binOpLeft left right f
    let inline binOpRight left right f = Core.binOpRight left right f
    

    // -------
    // Kleisli
    // -------

    let kleisli (g: Eff<'a, 'b, _, _>) (f: Gen<'a, _, _>): Gen<'b, _, _> =
        gen {
            let! f' = f
            return! g f' }

   
    // -----------
    // map / apply
    // -----------

    let map projection gen =
        fun s r ->
            let res = (run gen) s r
            let mappedRes = projection res.value
            { value = mappedRes
              state = res.state }
        |> Gen

    let apply (xGen: Gen<'v1, _, 'r>) (fGen: Gen<'v1 -> 'v2, _, 'r>): Gen<'v2, _, 'r> =
        gen {
            let! l' = xGen
            let! f' = fGen
            let result = f' l'
            return result
        }


    // ------
    // Reader
    // ------

    /// Reads the global state.
    let read () =
        fun _ r ->
            { value = r
              state = () }
        |> Gen


    // --------
    // Feedback / Init
    // --------

    let initValue seed f =
        fun s r ->
            let state = Option.defaultValue seed s
            f state r
        |> Gen

    let initWith seedFunc f =
        fun s r ->
            let state = Option.defaultWith seedFunc s
            f state r
        |> Gen

    let feedback seed (f: 'a -> 'r -> Gen<Res<'v, 'a>, 's, 'r>) =
        fun s r ->
            let feedbackState, innerState =
                match s with
                | None -> seed, None
                | Some (my, inner) -> my, inner

            let res = run (f feedbackState r) innerState r
            let feed = res.value
            let innerState = res.state
            { value = feed.value
              state = feed.state, Some innerState }
        |> Gen


module Operators =

    /// Feedback with reader state
    let (<|>) seed f = Gen.feedback seed f

    /// map operator
    let (<!>) gen projection = Gen.map projection gen

    /// apply operator
    let (<*>) fGen xGen = Gen.apply xGen fGen

    /// Kleisli operator (eff >> eff)
    let (>=>) f g = Eff.kleisli g f

    /// Kleisli "pipe" operator (gen >> eff)
    let (|=>) f g = Gen.kleisli g f
